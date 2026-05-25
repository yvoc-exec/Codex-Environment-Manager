using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CodexEnvironmentManager.Models;

namespace CodexEnvironmentManager.Services;

public class SessionManager
{
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

        TryWriteStopMarker(s);
        var killResult = KillSessionCore(s);
        var killed = killResult.KillConfirmed;
        var sessionRemoved = false;

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

    private static string GetSessionDirectory(Session session) =>
        Path.Combine(JunctionManager.SwitcherDir, "sessions", session.Id);

    private static bool HasManagedBestEffortArtifacts(Session session) =>
        !string.IsNullOrWhiteSpace(session.StartedMarkerPath) ||
        !string.IsNullOrWhiteSpace(session.ExitMarkerPath) ||
        !string.IsNullOrWhiteSpace(session.StopMarkerPath) ||
        Directory.Exists(GetSessionDirectory(session));

    public static bool ShouldPruneSession(Session session)
    {
        if (IsBestEffortDesktopSession(session))
            return !string.IsNullOrWhiteSpace(session.ExitMarkerPath) && File.Exists(session.ExitMarkerPath);

        if (!string.IsNullOrWhiteSpace(session.ExitMarkerPath) && File.Exists(session.ExitMarkerPath)) return true;
        if (!session.ProcessId.HasValue) return false;
        try { return Process.GetProcessById(session.ProcessId.Value).HasExited; }
        catch { return true; }
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
            return SessionKillResult.Build(
                session.Id,
                session.Type,
                targetResolved: true,
                targetProcessId: processId,
                killAttempted: true,
                killConfirmed: false,
                sessionRemoved: false,
                $"Failed to kill tracked process PID {processId}: {ex.Message}");
        }
    }

    private SessionKillResult KillBestEffortDesktopSession(Session session)
    {
        var inspection = _bestEffortDesktopTerminator.InspectBestEffortDesktopSession(session);

        if (!inspection.HasLiveDesktop)
        {
            if (!HasManagedBestEffortArtifacts(session))
            {
                return SessionKillResult.Build(
                    session.Id,
                    session.Type,
                    targetResolved: false,
                    targetProcessId: null,
                    killAttempted: false,
                    killConfirmed: true,
                    sessionRemoved: false,
                    $"Retired stale desktop placeholder session '{session.Id}' because no live desktop instance could be found.");
            }

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
                    : $"No live desktop instance could be found for session '{session.Id}', but managed artifacts remain. Leaving the session visible.");
        }

        if (inspection.IsAmbiguous || inspection.UniqueTarget == null)
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
            return p.ExitCode == 0;
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
                "$allowedRoots = @('cmd.exe','powershell.exe','pwsh.exe'); " +
                "$all = @(Get-CimInstance Win32_Process); " +
                "$roots = @($all | Where-Object { " +
                "  $_.CommandLine -and ($allowedRoots -contains $_.Name.ToLowerInvariant()) -and " +
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
                "if ($killed -eq 0 -and $script) { " +
                "  foreach ($r in @($roots | Where-Object { $_.CommandLine.Contains($script) })) { " +
                "    try { taskkill /PID $r.ProcessId /F | Out-Null; $killed++ } catch {} " +
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

            return p.ExitCode == 1;
        }
        catch
        {
            return false;
        }
    }

    public void PruneExitedSessions()
    {
        var dead = _active.Where(ShouldPruneSession).ToList();

        if (dead.Count == 0) return;
        foreach (var s in dead) _active.Remove(s);
        Save();
    }

    private void Save() => _config.SaveList("sessions", _active);
}
