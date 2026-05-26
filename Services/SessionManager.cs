using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CodexEnvironmentManager.Models;

namespace CodexEnvironmentManager.Services;

public enum SessionLiveState
{
    Live,
    ClearlyDead,
    Ambiguous
}

public sealed class SessionInspectionResult
{
    public SessionLiveState State { get; init; }
    public string Message { get; init; } = "";
    public int? TargetProcessId { get; init; }

    public static SessionInspectionResult Live(string message, int? targetProcessId = null) =>
        new()
        {
            State = SessionLiveState.Live,
            Message = message,
            TargetProcessId = targetProcessId
        };

    public static SessionInspectionResult ClearlyDead(string message) =>
        new()
        {
            State = SessionLiveState.ClearlyDead,
            Message = message
        };

    public static SessionInspectionResult Ambiguous(string message, int? targetProcessId = null) =>
        new()
        {
            State = SessionLiveState.Ambiguous,
            Message = message,
            TargetProcessId = targetProcessId
        };
}

public class SessionManager
{
    private static readonly TimeSpan DesktopLaunchSettleGrace = TimeSpan.FromSeconds(30);
    private static readonly IBestEffortDesktopTerminator s_staticDesktopTerminator = new CodexProcessManager(new LogService());
    private readonly ConfigService _config;
    private readonly IBestEffortDesktopTerminator _bestEffortDesktopTerminator;
    private readonly List<Session> _active = new();
    public IReadOnlyList<Session> Active => _active;

    public SessionManager(ConfigService config, IBestEffortDesktopTerminator bestEffortDesktopTerminator)
    {
        _config = config;
        ArgumentNullException.ThrowIfNull(bestEffortDesktopTerminator);
        _bestEffortDesktopTerminator = bestEffortDesktopTerminator;
        _active = _config.LoadList<Session>("sessions");
        PruneExitedSessions();
    }

    public void Register(Session s)
    {
        _active.RemoveAll(x => x.Id == s.Id);
        _active.Add(s);
        Save();
    }

    public void Remove(string sessionId)
    {
        _active.RemoveAll(x => x.Id == sessionId);
        Save();
    }

    public SessionKillResult KillSession(string sessionId, bool authoritativeRemovalRequested = false)
    {
        var s = _active.FirstOrDefault(x => x.Id == sessionId);
        if (s == null) return SessionKillResult.NotFound(sessionId);

        var inspection = InspectSession(s);
        if (inspection.State == SessionLiveState.ClearlyDead)
            return RetireStaleSession(s, inspection.Message);

        if (inspection.State == SessionLiveState.Ambiguous)
        {
            return SessionKillResult.Build(
                s.Id,
                s.Type,
                targetResolved: inspection.TargetProcessId.HasValue,
                targetProcessId: inspection.TargetProcessId,
                killAttempted: false,
                killConfirmed: false,
                sessionRemoved: false,
                inspection.Message);
        }

        TryWriteStopMarker(s);
        var killResult = KillSessionCore(s);
        var killed = killResult.KillConfirmed;
        var sessionRemoved = false;

        if (!killed)
        {
            // Fallback: if the session is clearly dead, retire the stale row.
            var postKillInspection = InspectSession(s);
            if (postKillInspection.State == SessionLiveState.ClearlyDead)
                return RetireStaleSession(s, postKillInspection.Message);
        }

        if (ShouldRemoveSessionAfterKill(killed, authoritativeRemovalRequested))
        {
            TryWriteExitMarker(s, "killed");
            _active.Remove(s);
            sessionRemoved = true;
        }
        Save();
        return SessionKillResult.Build(s.Id, s.Type, killResult.TargetResolved, killResult.TargetProcessId, killResult.KillAttempted, killed, sessionRemoved, killResult.Message);
    }

    public IReadOnlyList<SessionKillResult> KillAllDesktop(bool authoritativeRemovalRequested = false)
    {
        var results = new List<SessionKillResult>();
        foreach (var s in _active.Where(x => x.Type.StartsWith("desktop", StringComparison.OrdinalIgnoreCase)).ToList())
            results.Add(KillSession(s.Id, authoritativeRemovalRequested));

        return results;
    }

    public static bool ShouldRemoveSessionAfterKill(bool killConfirmed, bool authoritativeRemovalRequested = false) =>
        killConfirmed;

    public static bool IsBestEffortDesktopSession(Session session) =>
        session != null &&
        (session.IsBestEffortUntracked || string.Equals(session.Type, "desktop_store", StringComparison.OrdinalIgnoreCase));

    public static SessionLiveState ResolveSessionLiveState(Session session)
    {
        return InspectSession(session, s_staticDesktopTerminator).State;
    }

    public SessionInspectionResult InspectSession(Session session) =>
        InspectSession(session, _bestEffortDesktopTerminator);

    private static SessionInspectionResult InspectSession(Session session, IBestEffortDesktopTerminator desktopTerminator)
    {
        if (session == null)
            return SessionInspectionResult.ClearlyDead("No session was supplied.");

        if (IsBestEffortDesktopSession(session))
        {
            var inspection = desktopTerminator.InspectBestEffortDesktopSession(session);
            if (inspection.State == SessionLiveState.ClearlyDead && IsWithinDesktopLaunchGrace(session))
            {
                return SessionInspectionResult.Ambiguous(
                    "Desktop launch is still settling; waiting for Codex Desktop process to appear.");
            }

            return inspection.State switch
            {
                SessionLiveState.Live when inspection.TargetProcessId.HasValue
                    => SessionInspectionResult.Live(inspection.Message, inspection.TargetProcessId),
                SessionLiveState.Ambiguous
                    => SessionInspectionResult.Ambiguous(inspection.Message, inspection.TargetProcessId),
                _ => SessionInspectionResult.ClearlyDead(inspection.Message)
            };
        }

        if (!string.IsNullOrWhiteSpace(session.ExitMarkerPath) && File.Exists(session.ExitMarkerPath))
            return SessionInspectionResult.ClearlyDead($"Exit marker present for session '{session.Id}'.");

        if (session.ProcessId.HasValue)
        {
            try
            {
                var p = Process.GetProcessById(session.ProcessId.Value);
                return p.HasExited
                    ? SessionInspectionResult.ClearlyDead($"Tracked process PID {session.ProcessId.Value} is no longer running for session '{session.Id}'.")
                    : SessionInspectionResult.Live($"Tracked process PID {session.ProcessId.Value} is still running for session '{session.Id}'.", session.ProcessId.Value);
            }
            catch
            {
                return SessionInspectionResult.ClearlyDead($"Tracked process PID {session.ProcessId.Value} could not be found for session '{session.Id}'.");
            }
        }

        // For marker-tracked sessions (e.g., Windows Terminal mode where wrapper PID is not authoritative):
        var needle = !string.IsNullOrWhiteSpace(session.KillMarker) ? session.KillMarker : session.LauncherScriptPath;
        if (!string.IsNullOrWhiteSpace(needle))
        {
            var hasLiveProcess = HasLiveProcessByMarker(needle, session.LauncherScriptPath);
            if (hasLiveProcess)
            {
                return SessionInspectionResult.Live(
                    $"Marker-tracked CLI session '{session.Id}' still has live process evidence.");
            }

            var startedExists = !string.IsNullOrWhiteSpace(session.StartedMarkerPath) && File.Exists(session.StartedMarkerPath);
            if (startedExists || session.StartTime < DateTime.Now.AddSeconds(-30))
            {
                return SessionInspectionResult.ClearlyDead(
                    $"Marker-tracked CLI session '{session.Id}' has no live process evidence and is clearly closed.");
            }

            return SessionInspectionResult.Ambiguous(
                $"Marker-tracked CLI session '{session.Id}' has no live evidence yet; keeping the row visible until it settles.");
        }

        // No verifiable identity at all.
        return SessionInspectionResult.Ambiguous($"No verifiable identity could be resolved for session '{session.Id}'.");
    }

    private static bool IsWithinDesktopLaunchGrace(Session session)
    {
        if (session == null)
            return false;

        if (string.IsNullOrWhiteSpace(session.StartedMarkerPath) || !File.Exists(session.StartedMarkerPath))
            return false;

        return session.StartTime > DateTime.Now.Subtract(DesktopLaunchSettleGrace);
    }

    public static bool ShouldPruneSession(Session session)
    {
        return ResolveSessionLiveState(session) == SessionLiveState.ClearlyDead;
    }

    public void KillAll()
    {
        foreach (var s in _active.ToList())
            KillSession(s.Id);
    }

    private static void TryWriteStopMarker(Session s)
    {
        if (string.IsNullOrWhiteSpace(s.StopMarkerPath)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(s.StopMarkerPath)!);
            File.WriteAllText(s.StopMarkerPath, DateTime.Now.ToString("O"));
        }
        catch { /* non-fatal */ }
    }

    private static void TryWriteExitMarker(Session s, string status)
    {
        if (string.IsNullOrWhiteSpace(s.ExitMarkerPath)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(s.ExitMarkerPath)!);
            File.WriteAllText(s.ExitMarkerPath, status);
        }
        catch { /* non-fatal */ }
    }

    private SessionKillResult RetireStaleSession(Session session, string message)
    {
        _active.Remove(session);
        TryWriteExitMarker(session, "killed");
        Save();

        var finalMessage = string.IsNullOrWhiteSpace(message)
            ? $"Retired stale session '{session.Id}' because no live evidence remains."
            : $"Retired stale session '{session.Id}': {message}";

        return SessionKillResult.Build(
            session.Id,
            session.Type,
            targetResolved: false,
            targetProcessId: null,
            killAttempted: false,
            killConfirmed: true,
            sessionRemoved: true,
            finalMessage);
    }

    private SessionKillResult KillSessionCore(Session session)
    {
        if (IsBestEffortDesktopSession(session))
            return KillBestEffortDesktopSession(session);

        if (session.ProcessId.HasValue)
            return KillTrackedProcessSession(session);

        if (!string.IsNullOrWhiteSpace(session.CodexPidPath) && File.Exists(session.CodexPidPath))
        {
            var killed = KillRecordedCodexWrapperPid(session.CodexPidPath);
            return SessionKillResult.Build(
                session.Id,
                session.Type,
                targetResolved: true,
                targetProcessId: null,
                killAttempted: true,
                killConfirmed: killed,
                sessionRemoved: false,
                killed
                    ? $"Killed tracked wrapper process for session '{session.Id}'."
                    : $"Could not verify the wrapper kill for session '{session.Id}'.");
        }

        var killNeedle = !string.IsNullOrWhiteSpace(session.KillMarker) ? session.KillMarker : session.LauncherScriptPath;
        if (!string.IsNullOrWhiteSpace(killNeedle))
        {
            var killed = KillCliTabProcessByMarker(killNeedle, session.LauncherScriptPath);
            return SessionKillResult.Build(
                session.Id,
                session.Type,
                targetResolved: true,
                targetProcessId: null,
                killAttempted: true,
                killConfirmed: killed,
                sessionRemoved: false,
                killed
                    ? $"Killed CLI companion session '{session.Id}'."
                    : $"Could not verify CLI companion kill for session '{session.Id}'.");
        }

        return SessionKillResult.Build(
            session.Id,
            session.Type,
            targetResolved: false,
            targetProcessId: null,
            killAttempted: false,
            killConfirmed: false,
            sessionRemoved: false,
            $"No verifiable kill target could be resolved for session '{session.Id}'.");
    }

    private SessionKillResult KillTrackedProcessSession(Session session)
    {
        var processId = session.ProcessId!.Value;
        try
        {
            var p = Process.GetProcessById(processId);
            p.Kill(entireProcessTree: true);
            p.WaitForExit(3000);
            var confirmed = HasProcessExited(processId);
            return SessionKillResult.Build(
                session.Id,
                session.Type,
                targetResolved: true,
                targetProcessId: processId,
                killAttempted: true,
                killConfirmed: confirmed,
                sessionRemoved: false,
                confirmed
                    ? $"Killed tracked process PID {processId} for session '{session.Id}'."
                    : $"Could not verify tracked process PID {processId} exited for session '{session.Id}'.");
        }
        catch (Exception ex)
        {
            var alreadyGone = HasProcessExited(processId);
            return SessionKillResult.Build(
                session.Id,
                session.Type,
                targetResolved: true,
                targetProcessId: processId,
                killAttempted: true,
                killConfirmed: alreadyGone,
                sessionRemoved: false,
                alreadyGone
                    ? $"Tracked process PID {processId} was already gone for session '{session.Id}'."
                    : $"Failed to kill tracked process PID {processId}: {ex.Message}");
        }
    }

    private SessionKillResult KillBestEffortDesktopSession(Session session)
    {
        var inspection = _bestEffortDesktopTerminator.InspectBestEffortDesktopSession(session);

        if (inspection.State == SessionLiveState.ClearlyDead)
        {
            return SessionKillResult.Build(
                session.Id,
                session.Type,
                targetResolved: false,
                targetProcessId: null,
                killAttempted: false,
                killConfirmed: true,
                sessionRemoved: false,
                inspection.Message);
        }

        if (inspection.State == SessionLiveState.Ambiguous || inspection.UniqueTarget == null)
        {
            return SessionKillResult.Build(
                session.Id,
                session.Type,
                targetResolved: false,
                targetProcessId: null,
                killAttempted: false,
                killConfirmed: false,
                sessionRemoved: false,
                inspection.Message.Length > 0
                    ? inspection.Message
                    : $"Multiple desktop candidates were detected for session '{session.Id}'. Leaving the session visible.");
        }

        var result = _bestEffortDesktopTerminator.TryKillBestEffortDesktopSession(session);
        return SessionKillResult.Build(
            session.Id,
            session.Type,
            result.TargetResolved,
            result.TargetProcessId,
            result.KillAttempted,
            result.KillConfirmed,
            false,
            result.Message);
    }

    private static bool HasProcessExited(int processId)
    {
        try
        {
            return Process.GetProcessById(processId).HasExited;
        }
        catch
        {
            return true;
        }
    }

    private static bool KillRecordedCodexWrapperPid(string pidPath)
    {
        try
        {
            var text = File.ReadAllText(pidPath).Trim();
            if (!int.TryParse(text, out var pid) || pid <= 0) return false;

            var psi = new ProcessStartInfo
            {
                FileName = "taskkill.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("/PID");
            psi.ArgumentList.Add(pid.ToString());
            psi.ArgumentList.Add("/T");
            psi.ArgumentList.Add("/F");

            var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(7000);
            if (p.ExitCode == 0) return true;

            // Fallback: verify if the process is actually gone even if taskkill reported failure.
            return HasProcessExited(pid);
        }
        catch
        {
            return false;
        }
    }

    private static bool KillCliTabProcessByMarker(string marker, string? launcherScriptPath)
    {
        try
        {
            var markerEscaped = marker.Replace("'", "''");
            var scriptEscaped = (launcherScriptPath ?? "").Replace("'", "''");

            var psCommand =
                "$marker = '" + markerEscaped + "'; " +
                "$script = '" + scriptEscaped + "'; " +
                "$self = $PID; " +
                "$allowedRoots = @('cmd.exe','powershell.exe','pwsh.exe'); " +
                "$all = @(Get-CimInstance Win32_Process); " +
                "$roots = @($all | Where-Object { " +
                "  $_.ProcessId -ne $self -and $_.CommandLine -and ($allowedRoots -contains $_.Name.ToLowerInvariant()) -and " +
                "  (($_.CommandLine.Contains($marker)) -or ($script -and $_.CommandLine.Contains($script))) " +
                "}); " +
                "$targets = New-Object System.Collections.Generic.HashSet[int]; " +
                "function Add-Children([int]$pid) { " +
                "  foreach ($c in @($all | Where-Object { $_.ParentProcessId -eq $pid })) { " +
                "    if ($targets.Add([int]$c.ProcessId)) { Add-Children ([int]$c.ProcessId) } " +
                "  } " +
                "} " +
                "foreach ($r in $roots) { Add-Children ([int]$r.ProcessId) } " +
                "$killed = 0; " +
                "foreach ($pid in $targets) { " +
                "  try { taskkill /PID $pid /T /F | Out-Null; $killed++ } catch {} " +
                "}; " +
                "if ($killed -eq 0) { " +
                "  foreach ($r in $roots) { " +
                "    try { taskkill /PID $r.ProcessId /T /F | Out-Null; $killed++ } catch {} " +
                "  } " +
                "}; " +
                "exit ([Math]::Min($killed, 1))";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add(psCommand);

            var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(7000);

            if (p.ExitCode == 1) return true;

            // Fallback: verify if any matching process still lives.
            return !HasLiveProcessByMarker(marker, launcherScriptPath);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasLiveProcessByMarker(string marker, string? launcherScriptPath)
    {
        try
        {
            var markerEscaped = marker.Replace("'", "''");
            var scriptEscaped = (launcherScriptPath ?? "").Replace("'", "''");

            var psCommand =
                "$marker = '" + markerEscaped + "'; " +
                "$script = '" + scriptEscaped + "'; " +
                "$self = $PID; " +
                "$allowedRoots = @('cmd.exe','powershell.exe','pwsh.exe'); " +
                "$all = @(Get-CimInstance Win32_Process); " +
                "$roots = @($all | Where-Object { " +
                "  $_.ProcessId -ne $self -and $_.CommandLine -and ($allowedRoots -contains $_.Name.ToLowerInvariant()) -and " +
                "  (($_.CommandLine.Contains($marker)) -or ($script -and $_.CommandLine.Contains($script))) " +
                "}); " +
                "$targets = New-Object System.Collections.Generic.HashSet[int]; " +
                "function Add-Children([int]$pid) { " +
                "  foreach ($c in @($all | Where-Object { $_.ParentProcessId -eq $pid })) { " +
                "    if ($targets.Add([int]$c.ProcessId)) { Add-Children ([int]$c.ProcessId) } " +
                "  } " +
                "} " +
                "foreach ($r in $roots) { Add-Children ([int]$r.ProcessId) } " +
                "if ($roots.Count -gt 0 -or $targets.Count -gt 0) { exit 1 } else { exit 0 }";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add(psCommand);

            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(7000);
            return p.ExitCode == 1;
        }
        catch
        {
            return false;
        }
    }

    public void PruneExitedSessions()
    {
        var dead = _active.Where(s => InspectSession(s).State == SessionLiveState.ClearlyDead).ToList();

        if (dead.Count == 0) return;
        foreach (var s in dead) _active.Remove(s);
        Save();
    }

    private void Save() => _config.SaveList("sessions", _active);
}
