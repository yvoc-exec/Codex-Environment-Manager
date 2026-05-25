using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using CodexEnvironmentManager.Models;

namespace CodexEnvironmentManager.Services;

public sealed class CodexDesktopDetection
{
    public bool HasExecutable { get; init; }
    public string? ExecutablePath { get; init; }
    public bool HasStoreApp { get; init; }
    public string? StorePackageFamilyName { get; init; }
    public string? StoreInstallLocation { get; init; }
    public bool HasCliFallback { get; init; }
    public string? CliPath { get; init; }
    public bool HasProtocol { get; init; }
    public string? ProtocolSource { get; init; }
    public string? ProtocolCommand { get; init; }

    public bool CanLaunch => HasExecutable || HasCliFallback;

    public string InstallKind
    {
        get
        {
            if (HasExecutable) return "Win32/installer executable";
            if (HasStoreApp && HasCliFallback) return "Microsoft Store app via codex app";
            if (HasStoreApp) return "Microsoft Store app detected; CLI fallback missing";
            if (HasCliFallback) return "CLI fallback only";
            return "Not found";
        }
    }

    public string DisplayText
    {
        get
        {
            if (HasExecutable) return $"Win32/installer app detected: {ExecutablePath}";
            if (HasStoreApp && HasCliFallback) return $"Microsoft Store app detected ({StorePackageFamilyName}); launch via codex app";
            if (HasStoreApp) return $"Microsoft Store app detected ({StorePackageFamilyName}); install Codex CLI for managed launch";
            if (HasCliFallback) return "Codex CLI fallback detected: codex app";
            return "Not found — install Codex app or browse manually";
        }
    }
}

public class CodexProcessManager : IBestEffortDesktopTerminator
{
    private readonly LogService _log;
    public string? OverridePath { get; set; }

    public CodexProcessManager(LogService log) => _log = log;

    public BestEffortDesktopSessionInspection InspectBestEffortDesktopSession(Session session)
    {
        if (session == null)
            return BestEffortDesktopSessionInspection.NoDesktop("No desktop session was supplied.");

        var candidates = DesktopProcessTargetResolver.GetPlausibleCandidates(GetCodexProcessSnapshots());
        if (candidates.Count == 0)
            return BestEffortDesktopSessionInspection.NoDesktop($"No live desktop instance was detected for session '{session.Id}'.");

        var target = DesktopProcessTargetResolver.Resolve(candidates.Select(x => x.Process));
        if (target != null)
            return BestEffortDesktopSessionInspection.Unique(target, $"Resolved unique desktop target PID {target.ProcessId} for session '{session.Id}'.");

        return BestEffortDesktopSessionInspection.Ambiguous($"Multiple desktop candidates were detected for session '{session.Id}'.");
    }

    public DesktopKillAttemptResult TryKillBestEffortDesktopSession(Session session)
    {
        var inspection = InspectBestEffortDesktopSession(session);
        if (inspection.UniqueTarget == null)
            return DesktopKillAttemptResult.Inconclusive(inspection.Message);

        return TryKillDesktopProcess(inspection.UniqueTarget.ProcessId);
    }

    private DesktopKillAttemptResult TryKillDesktopProcess(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            _log.Info($"Killing targeted Codex Desktop process PID {processId}");
            process.CloseMainWindow();
            if (!process.WaitForExit(3000))
                process.Kill(entireProcessTree: true);

            if (!WaitForProcessExit(processId, 3000))
                return DesktopKillAttemptResult.Failed(processId, $"Targeted desktop process PID {processId} could not be verified as terminated.");

            return DesktopKillAttemptResult.Confirmed(processId, $"Targeted desktop process PID {processId} terminated.");
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to kill targeted Codex Desktop PID {processId}: {ex.Message}");
            return DesktopKillAttemptResult.Failed(processId, $"Failed to kill targeted desktop process PID {processId}: {ex.Message}");
        }
    }

    private static bool WaitForProcessExit(int processId, int timeoutMs)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return process.WaitForExit(timeoutMs);
        }
        catch
        {
            return true;
        }
    }

    private IReadOnlyList<CodexProcessSnapshot> GetCodexProcessSnapshots()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add("$p = Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'Codex*' } | Select-Object ProcessId, Name, CommandLine; foreach ($item in $p) { Write-Output ($item.ProcessId.ToString() + '|' + $item.Name + '|' + ($item.CommandLine -replace '\\|','/')) }");

            using var proc = Process.Start(psi);
            if (proc == null) return Array.Empty<CodexProcessSnapshot>();

            if (!proc.WaitForExit(3000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                _log.Warn("Timed out while enumerating Codex processes for desktop kill.");
                return Array.Empty<CodexProcessSnapshot>();
            }

            var snapshots = new System.Collections.Generic.List<CodexProcessSnapshot>();
            foreach (var line in proc.StandardOutput.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|', 3);
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[0], out var pid)) continue;
                var name = parts[1];
                var commandLine = parts.Length > 2 ? parts[2] : null;
                snapshots.Add(new CodexProcessSnapshot(pid, name, commandLine));
            }

            return snapshots;
        }
        catch (Exception ex)
        {
            _log.Warn($"Codex desktop process enumeration failed: {ex.Message}");
            return Array.Empty<CodexProcessSnapshot>();
        }
    }

    public CodexDesktopDetection DetectCodexDesktop()
    {
        TryFindCodexDesktopExe(out var exePath);
        TryDetectStoreApp(out var packageFamilyName, out var installLocation);
        TryFindCodexCliExecutable(out var cliPath);
        TryDetectCodexProtocol(out var protocolSource, out var protocolCommand);

        return new CodexDesktopDetection
        {
            HasExecutable = !string.IsNullOrWhiteSpace(exePath),
            ExecutablePath = exePath,
            HasStoreApp = !string.IsNullOrWhiteSpace(packageFamilyName),
            StorePackageFamilyName = packageFamilyName,
            StoreInstallLocation = installLocation,
            HasCliFallback = !string.IsNullOrWhiteSpace(cliPath),
            CliPath = cliPath,
            HasProtocol = !string.IsNullOrWhiteSpace(protocolSource),
            ProtocolSource = protocolSource,
            ProtocolCommand = protocolCommand
        };
    }

    public bool TryDetectCodexProtocol(out string? source, out string? command)
    {
        source = null;
        command = null;

        foreach (var candidate in new[]
        {
            (Registry.CurrentUser, @"Software\Classes\codex", "HKCU\\Software\\Classes\\codex"),
            (Registry.ClassesRoot, "codex", "HKCR\\codex")
        })
        {
            try
            {
                using var root = candidate.Item1.OpenSubKey(candidate.Item2);
                if (root == null) continue;

                var urlProtocol = root.GetValue("URL Protocol") != null;
                using var commandKey = root.OpenSubKey(@"shell\open\command");
                var commandValue = commandKey?.GetValue(null) as string;
                if (urlProtocol || !string.IsNullOrWhiteSpace(commandValue))
                {
                    source = candidate.Item3;
                    command = commandValue?.Trim();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Codex protocol detection failed for {candidate.Item3}: {ex.Message}");
            }
        }

        return false;
    }

    public string FindCodexDesktopExe()
    {
        if (TryFindCodexDesktopExe(out var path) && !string.IsNullOrWhiteSpace(path))
            return path;

        _log.Error("Codex Desktop executable not found in any known Win32/manual installer location.");
        throw new FileNotFoundException("Codex Desktop executable not found. Store-installed Codex can still be launched through Codex CLI using `codex app`.");
    }

    public bool TryFindCodexDesktopExe(out string? path)
    {
        path = null;

        if (!string.IsNullOrEmpty(OverridePath) && File.Exists(OverridePath))
        {
            path = OverridePath;
            _log.Info($"Using Codex Desktop override path: {OverridePath}");
            return true;
        }

        foreach (var c in GetDesktopExecutableCandidates())
        {
            try
            {
                if (File.Exists(c))
                {
                    path = c;
                    _log.Info($"Found Codex Desktop executable: {c}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed checking Codex Desktop candidate '{c}': {ex.Message}");
            }
        }

        foreach (var registryPath in GetRegistryAppPaths())
        {
            if (!string.IsNullOrWhiteSpace(registryPath) && File.Exists(registryPath))
            {
                path = registryPath;
                _log.Info($"Found Codex Desktop via registry App Paths: {registryPath}");
                return true;
            }
        }

        return false;
    }

    private static string[] GetDesktopExecutableCandidates()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var windowsAppsAlias = Path.Combine(local, "Microsoft", "WindowsApps");

        return new[]
        {
            Path.Combine(local, "Programs", "Codex", "Codex.exe"),
            Path.Combine(local, "Programs", "OpenAI Codex", "Codex.exe"),
            Path.Combine(local, "Programs", "OpenAI", "Codex", "Codex.exe"),
            Path.Combine(local, "Codex", "Codex.exe"),
            Path.Combine(programFiles, "Codex", "Codex.exe"),
            Path.Combine(programFiles, "OpenAI Codex", "Codex.exe"),
            Path.Combine(programFiles, "OpenAI", "Codex", "Codex.exe"),
            Path.Combine(programFilesX86, "Codex", "Codex.exe"),
            Path.Combine(programFilesX86, "OpenAI Codex", "Codex.exe"),
            Path.Combine(programFilesX86, "OpenAI", "Codex", "Codex.exe"),
            Path.Combine(windowsAppsAlias, "Codex.exe")
        }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string[] GetRegistryAppPaths()
    {
        var results = new System.Collections.Generic.List<string>();
        var subKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Codex.exe";
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var key = hive.OpenSubKey(subKey);
                if (key?.GetValue(null) is string regPath)
                    results.Add(regPath);
            }
            catch
            {
                // Ignore registry access errors; detection has other fallbacks.
            }
        }
        return results.ToArray();
    }

    public bool TryDetectStoreApp(out string? packageFamilyName, out string? installLocation)
    {
        packageFamilyName = null;
        installLocation = null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add("$p = Get-AppxPackage -Name OpenAI.Codex -ErrorAction SilentlyContinue | Select-Object -First 1; if ($p) { Write-Output ($p.PackageFamilyName + '|' + $p.InstallLocation) }");

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            if (!proc.WaitForExit(3000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                _log.Warn("Timed out while checking Microsoft Store Codex package.");
                return false;
            }

            var output = proc.StandardOutput.ReadToEnd().Trim();
            if (string.IsNullOrWhiteSpace(output)) return false;

            var parts = output.Split('|', 2);
            packageFamilyName = parts.Length > 0 ? parts[0].Trim() : null;
            installLocation = parts.Length > 1 ? parts[1].Trim() : null;
            if (!string.IsNullOrWhiteSpace(packageFamilyName))
            {
                _log.Info($"Found Codex Microsoft Store package: {packageFamilyName} at {installLocation}");
                return true;
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Microsoft Store Codex detection failed: {ex.Message}");
        }

        return false;
    }




    public bool CanLaunchCodexDesktopViaCli()
    {
        return TryFindCodexCliExecutable(out _);
    }

    public ProcessStartInfo CreateCodexAppProcessStartInfo(string accountPath, string workingDirectory)
    {
        if (!TryFindCodexCliExecutable(out var codexPath) || string.IsNullOrWhiteSpace(codexPath))
            throw new FileNotFoundException("Codex Desktop executable was not found and Codex CLI is not available for `codex app` fallback.");

        ProcessStartInfo psi;
        if (codexPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || codexPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                WorkingDirectory = workingDirectory
            };
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(BuildBatchCommand(codexPath, new[] { "app", workingDirectory }));
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = codexPath,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory
            };
            psi.ArgumentList.Add("app");
            psi.ArgumentList.Add(workingDirectory);
        }

        psi.EnvironmentVariables["CODEX_HOME"] = accountPath;
        return psi;
    }

    public static string? FindWindowsTerminal()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var trimmed = dir.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            var wt = Path.Combine(trimmed, "wt.exe");
            if (File.Exists(wt)) return wt;
        }
        var localApps = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var wt2 = Path.Combine(localApps, "Microsoft", "WindowsApps", "wt.exe");
        if (File.Exists(wt2)) return wt2;
        return null;
    }

    public static bool IsCodexCliInstalled() => TryFindCodexCliExecutable(out _);

    public static bool TryFindCodexCliExecutable(out string? path)
    {
        path = null;
        foreach (var candidate in GetCodexCliCandidates())
        {
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }
        }
        return false;
    }

    private static string[] GetCodexCliCandidates()
    {
        var candidates = new System.Collections.Generic.List<string>();
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var trimmed = dir.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            candidates.Add(Path.Combine(trimmed, "codex.exe"));
            candidates.Add(Path.Combine(trimmed, "codex.cmd"));
            candidates.Add(Path.Combine(trimmed, "codex"));
        }

        var npmGlobal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm");
        candidates.Add(Path.Combine(npmGlobal, "codex.cmd"));
        candidates.Add(Path.Combine(npmGlobal, "codex.exe"));

        var localBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
        candidates.Add(Path.Combine(localBin, "codex.exe"));
        candidates.Add(Path.Combine(localBin, "codex.cmd"));

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static ProcessStartInfo CreateCodexCliProcessStartInfo(string codexPath, string codeHome, params string[] args)
    {
        ProcessStartInfo psi;
        if (codexPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || codexPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
        {
            psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false
            };
            psi.ArgumentList.Add("/d");
            psi.ArgumentList.Add("/s");
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add("call " + BuildBatchCommand(codexPath, args));
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = codexPath,
                UseShellExecute = false
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);
        }

        psi.EnvironmentVariables["CODEX_HOME"] = codeHome;
        return psi;
    }

    private static string BuildBatchCommand(string executable, string[] args)
    {
        var parts = new[] { QuoteForCmd(executable) }.Concat(args.Select(QuoteForCmd));
        return string.Join(" ", parts);
    }

    public static string QuoteForCmd(string arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        var escaped = EscapeBatchToken(arg);
        return $"\"{escaped}\"";
    }

    public static string EscapeBatchValue(string value) => EscapeBatchToken(value ?? string.Empty);

    public static string? NormalizeDesktopOverridePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : path.Trim();

    private static string EscapeBatchToken(string value) =>
        (value ?? string.Empty)
            .Replace("^", "^^")
            .Replace("%", "%%")
            .Replace("!", "^!")
            .Replace("\"", "^\"")
            .Replace("&", "^&")
            .Replace("|", "^|")
            .Replace("<", "^<")
            .Replace(">", "^>");
}
