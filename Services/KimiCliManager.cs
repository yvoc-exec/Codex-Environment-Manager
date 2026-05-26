using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CodexEnvironmentManager.Models;

namespace CodexEnvironmentManager.Services;

public sealed class KimiLaunchSetup
{
    public string SessionDirectory { get; init; } = "";
    public string LaunchScriptPath { get; init; } = "";
    public string AgentFilePath { get; init; } = "";
    public string PromptFilePath { get; init; } = "";
    public string RoleTemplatePath { get; init; } = "";
    public string KimiExecutablePath { get; init; } = "";
    public string[] ExtraArgs { get; init; } = Array.Empty<string>();
    public string[] LaunchArgs { get; init; } = Array.Empty<string>();
    public KimiProfileOptions KimiOptions { get; init; } = new();
    public bool UsesWindowsTerminal { get; init; }
    public ProcessStartInfo StartInfo { get; init; } = new();
}

public sealed class KimiCliManager
{
    private readonly ConfigService _config;
    private readonly LogService _log;

    public KimiCliManager(ConfigService config, LogService log)
    {
        _config = config;
        _log = log;
    }

    public string BuildLaunchPreview(Account acct, Persona? persona, Workspace workspace)
    {
        var settings = LoadSettings();
        var resolvedPath = TryResolveKimiCliExecutable(settings, out var sourcePath)
            ? sourcePath
            : "<not found>";
        if (persona == null)
            throw new InvalidOperationException("Selected profile is invalid.");
        var migration = NormalizeKimiPersonaOrThrow(persona);
        EnsureKimiLaunchReady(persona, workspace.Path, "<CEM-generated-session-agent.yaml>", validateWorkspace: true);
        var personaLabel = string.IsNullOrWhiteSpace(persona.Name) ? "(no persona selected)" : persona.Name;
        var modelLabel = string.IsNullOrWhiteSpace(persona.Model) ? "(default Kimi model)" : persona.Model;
        var roleTemplate = ResolveRoleTemplatePath(persona.AgentsTemplatePath);
        var kimiHome = GetKimiCodeHome(acct);
        var previewArgs = BuildKimiLaunchArgs(persona, workspace.Path, "<CEM-generated-session-agent.yaml>", validatePaths: true);

        var sb = new StringBuilder();
        sb.AppendLine("Launch type: kimi-cli");
        sb.AppendLine();
        sb.AppendLine($"Account: {acct.Name}");
        sb.AppendLine($"KIMI_CODE_HOME: {kimiHome}");
        sb.AppendLine();
        sb.AppendLine($"Kimi CLI: {resolvedPath}");
        sb.AppendLine($"Workspace: {workspace.Name}");
        sb.AppendLine(workspace.Path);
        sb.AppendLine();
        sb.AppendLine($"Selected CEM profile: {personaLabel}");
        sb.AppendLine($"Model: {modelLabel}");
        if (!string.IsNullOrWhiteSpace(roleTemplate))
            sb.AppendLine($"Role template: {roleTemplate}");
        AppendKimiOptionSummary(sb, persona.KimiOptions);
        AppendKimiMigrationNotes(sb, migration);
        sb.AppendLine();
        sb.AppendLine("CLI contract:");
        sb.AppendLine("kimi " + string.Join(" ", previewArgs.Select(CodexProcessManager.QuoteForCmd)));
        sb.AppendLine();
        sb.AppendLine("Kimi auth/config is isolated per account under KIMI_CODE_HOME.");
        sb.AppendLine("CEM writes only per-session Kimi agent artifacts outside Codex account/profile storage.");
        return sb.ToString();
    }

    public void RunLogin(Account acct)
    {
        var psi = CreateLoginStartInfo(acct);
        _log.Info($"Launching Kimi login/setup flow for account '{acct.Name}' with KIMI_CODE_HOME='{GetKimiCodeHome(acct)}'.");
        var proc = Process.Start(psi);
        if (proc == null)
            throw new InvalidOperationException("Unable to start Kimi login/setup.");
    }

    public ProcessStartInfo CreateLoginStartInfo(Account acct)
    {
        var settings = LoadSettings();
        var kimiPath = ResolveKimiCliExecutableOrThrow(settings);
        var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var wtPath = CodexProcessManager.FindWindowsTerminal();
        var kimiHome = GetKimiCodeHome(acct);
        var loginCommand = BuildKimiLoginCommand(kimiPath, kimiHome);

        if (!string.IsNullOrWhiteSpace(wtPath))
        {
            var psi = new ProcessStartInfo
            {
                FileName = wtPath,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory
            };
            psi.ArgumentList.Add("-w");
            psi.ArgumentList.Add("CodexEnvironmentManager");
            psi.ArgumentList.Add("new-tab");
            psi.ArgumentList.Add("--title");
            psi.ArgumentList.Add("Kimi Login / Setup");
            psi.ArgumentList.Add("cmd.exe");
            psi.ArgumentList.Add("/k");
            psi.ArgumentList.Add(loginCommand);
            psi.EnvironmentVariables["KIMI_CODE_HOME"] = kimiHome;
            psi.EnvironmentVariables["KIMI_SHARE_DIR"] = kimiHome;
            return psi;
        }

        var fallbackPsi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
            ArgumentList =
            {
                "/k",
                loginCommand
            }
        };
        fallbackPsi.EnvironmentVariables["KIMI_CODE_HOME"] = kimiHome;
        fallbackPsi.EnvironmentVariables["KIMI_SHARE_DIR"] = kimiHome;
        return fallbackPsi;
    }

    public KimiLaunchSetup PrepareLaunch(string sessionId, Account acct, Persona? persona, Workspace workspace, bool preferWindowsTerminal)
    {
        var settings = LoadSettings();
        var kimiPath = ResolveKimiCliExecutableOrThrow(settings);
        var roleTemplatePath = ResolveRoleTemplatePath(persona?.AgentsTemplatePath);

        var sessionDirectory = Path.Combine(JunctionManager.SwitcherDir, "sessions", sessionId);
        Directory.CreateDirectory(sessionDirectory);
        var kimiHome = GetKimiCodeHome(acct);

        var generatedFiles = KimiAgentFileBuilder.CreateSessionFiles(
            _config.GeneratedDir,
            sessionId,
            persona?.Name,
            workspace.Name,
            workspace.Path,
            roleTemplatePath);

        if (persona == null)
            throw new InvalidOperationException("Selected profile is invalid.");

        var migration = NormalizeKimiPersonaOrThrow(persona);
        EnsureKimiLaunchReady(persona, workspace.Path, generatedFiles.AgentFilePath, validateWorkspace: true);
        var launchArgs = BuildKimiLaunchArgs(persona, workspace.Path, generatedFiles.AgentFilePath, validatePaths: true);
        if (migration.Warnings.Count > 0)
            _log.Info("Kimi profile migration notes: " + string.Join(" | ", migration.Warnings));
        var launchScriptPath = CreateLaunchScript(
            sessionDirectory,
            sessionId,
            kimiPath,
            workspace.Path,
            workspace.Name,
            generatedFiles.AgentFilePath,
            generatedFiles.PromptFilePath,
            launchArgs,
            kimiHome);

        var launchInfo = CreateLauncherProcessStartInfo(
            preferWindowsTerminal,
            sessionId,
            persona?.Name,
            workspace.Name,
            workspace.Path,
            launchScriptPath);

        launchInfo.ProcessStartInfo.EnvironmentVariables["KIMI_CODE_HOME"] = GetKimiCodeHome(acct);
        launchInfo.ProcessStartInfo.EnvironmentVariables["KIMI_SHARE_DIR"] = kimiHome;

        // Inject Moonshot API key if applicable.
        // Kimi Code CLI recognizes KIMI_API_KEY as the documented environment override.
        if (string.Equals(acct.Type, "moonshot_api_key", StringComparison.OrdinalIgnoreCase))
        {
            var key = !string.IsNullOrWhiteSpace(acct.ApiKeyEncrypted)
                ? DpapiHelper.DecryptFromBase64(acct.ApiKeyEncrypted)
                : null;
            if (!string.IsNullOrWhiteSpace(key))
            {
                launchInfo.ProcessStartInfo.EnvironmentVariables["KIMI_API_KEY"] = key;
            }
        }

        return new KimiLaunchSetup
        {
            SessionDirectory = sessionDirectory,
            LaunchScriptPath = launchScriptPath,
            AgentFilePath = generatedFiles.AgentFilePath,
            PromptFilePath = generatedFiles.PromptFilePath,
            RoleTemplatePath = roleTemplatePath ?? "",
            KimiExecutablePath = kimiPath,
            ExtraArgs = launchArgs,
            LaunchArgs = launchArgs,
            KimiOptions = persona!.KimiOptions,
            UsesWindowsTerminal = launchInfo.UsesWindowsTerminal,
            StartInfo = launchInfo.ProcessStartInfo
        };
    }

    public bool TryResolveKimiCliExecutable(out string? path)
    {
        var settings = LoadSettings();
        return TryResolveKimiCliExecutable(settings, out path);
    }

    public static string GetKimiCodeHome(Account acct) =>
        JunctionManager.GetKimiAccountHomePath(acct.Id);

    private static (ProcessStartInfo ProcessStartInfo, bool UsesWindowsTerminal) CreateLauncherProcessStartInfo(
        bool preferWindowsTerminal,
        string sessionId,
        string? personaName,
        string workspaceName,
        string workspacePath,
        string launchScriptPath)
    {
        var killMarker = $"CEM_KIMI_SESSION_{sessionId}";
        var titlePersona = string.IsNullOrWhiteSpace(personaName) ? "Kimi" : personaName.Trim();

        ProcessStartInfo psi;
        if (preferWindowsTerminal)
        {
            var wtPath = CodexProcessManager.FindWindowsTerminal();
            if (!string.IsNullOrWhiteSpace(wtPath))
            {
                psi = new ProcessStartInfo
                {
                    FileName = wtPath,
                    UseShellExecute = false,
                    WorkingDirectory = workspacePath
                };
                psi.ArgumentList.Add("-w");
                psi.ArgumentList.Add("CodexEnvironmentManager");
                psi.ArgumentList.Add("new-tab");
                psi.ArgumentList.Add("--title");
                psi.ArgumentList.Add($"CEM {sessionId[..8]} [{titlePersona}] - {workspaceName}");
                psi.ArgumentList.Add("cmd.exe");
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add($"set CEM_SESSION_MARKER={killMarker} && call \"{launchScriptPath}\"");
                return (psi, true);
            }
        }

        psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            WorkingDirectory = workspacePath
        };
        psi.ArgumentList.Add("/k");
        psi.ArgumentList.Add($"set CEM_SESSION_MARKER={killMarker} && call \"{launchScriptPath}\"");
        return (psi, false);
    }

    private string CreateLaunchScript(
        string sessionDirectory,
        string sessionId,
        string kimiPath,
        string workspacePath,
        string workspaceName,
        string agentFilePath,
        string promptFilePath,
        string[] launchArgs,
        string kimiHome)
    {
        var scriptPath = Path.Combine(sessionDirectory, "launch.cmd");
        var runnerPath = Path.Combine(sessionDirectory, "run_kimi_wrapper.ps1");
        var exitMarker = Path.Combine(sessionDirectory, "exit.txt");
        var startedMarker = Path.Combine(sessionDirectory, "started.txt");
        var stopMarker = Path.Combine(sessionDirectory, "stop.txt");

        WriteKimiPowerShellWrapper(
            runnerPath,
            kimiPath,
            workspacePath,
            agentFilePath,
            launchArgs,
            stopMarker);

        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("setlocal");
        sb.AppendLine($"title Kimi [{LauncherService.EscapeBatchTitle(workspaceName)}]");
        sb.AppendLine("chcp 65001 > nul");
        sb.AppendLine($"cd /d \"{CodexProcessManager.EscapeBatchValue(workspacePath)}\"");
        sb.AppendLine("if errorlevel 1 exit /b 1");
        sb.AppendLine($"set \"KIMI_CODE_HOME={CodexProcessManager.EscapeBatchValue(kimiHome)}\"");
        sb.AppendLine($"set \"KIMI_SHARE_DIR={CodexProcessManager.EscapeBatchValue(kimiHome)}\"");
        sb.AppendLine("set \"PYTHONIOENCODING=utf-8\"");
        sb.AppendLine("set \"PYTHONUTF8=1\"");
        sb.AppendLine($"set \"CEM_SESSION_MARKER=CEM_KIMI_SESSION_{CodexProcessManager.EscapeBatchValue(sessionId)}\"");
        sb.AppendLine($"set \"CEM_KIMI_SESSION_ID={CodexProcessManager.EscapeBatchValue(sessionId)}\"");
        sb.AppendLine($"set \"CEM_KIMI_AGENT_FILE={CodexProcessManager.EscapeBatchValue(agentFilePath)}\"");
        sb.AppendLine($"set \"CEM_KIMI_PROMPT_FILE={CodexProcessManager.EscapeBatchValue(promptFilePath)}\"");
        sb.AppendLine($"echo started > \"{CodexProcessManager.EscapeBatchValue(startedMarker)}\"");
        sb.AppendLine("echo.");
        sb.AppendLine($@"powershell.exe -NoProfile -ExecutionPolicy Bypass -File ""{CodexProcessManager.EscapeBatchValue(runnerPath)}""");
        sb.AppendLine("set KIMI_EXIT_CODE=%ERRORLEVEL%");
        sb.AppendLine($"if exist \"{CodexProcessManager.EscapeBatchValue(stopMarker)}\" (");
        sb.AppendLine($@"  echo killed > ""{CodexProcessManager.EscapeBatchValue(exitMarker)}""");
        sb.AppendLine("  echo.");
        sb.AppendLine("  echo [CEM] Stop marker detected. Closing this Kimi session.");
        sb.AppendLine("  endlocal");
        sb.AppendLine("  exit 0");
        sb.AppendLine(")");
        sb.AppendLine($@"echo %KIMI_EXIT_CODE% > ""{CodexProcessManager.EscapeBatchValue(exitMarker)}""");
        sb.AppendLine("echo.");
        sb.AppendLine("echo Kimi exited with code %KIMI_EXIT_CODE%.");
        sb.AppendLine("endlocal");
        sb.AppendLine("exit /b %KIMI_EXIT_CODE%");

        File.WriteAllText(scriptPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return scriptPath;
    }

    private static void WriteKimiPowerShellWrapper(
        string runnerPath,
        string kimiPath,
        string workspacePath,
        string agentFilePath,
        string[] launchArgs,
        string stopMarker)
    {
        var kimiArgs = string.Join(", ", launchArgs.Select(arg => "'" + EscapePowerShellSingleQuoted(arg) + "'"));

        var ps = new StringBuilder();
        ps.AppendLine("$ErrorActionPreference = 'Stop'");
        ps.AppendLine("$kimiPath = '" + EscapePowerShellSingleQuoted(kimiPath) + "'");
        ps.AppendLine("$kimiArgs = @(" + kimiArgs + ")");
        ps.AppendLine("$stopMarker = '" + EscapePowerShellSingleQuoted(stopMarker) + "'");
        ps.AppendLine("try {");
        ps.AppendLine("  & $kimiPath @kimiArgs");
        ps.AppendLine("  $exitCode = $LASTEXITCODE");
        ps.AppendLine("  if (Test-Path -LiteralPath $stopMarker) { exit 0 }");
        ps.AppendLine("  exit $exitCode");
        ps.AppendLine("} catch {");
        ps.AppendLine("  if (Test-Path -LiteralPath $stopMarker) { exit 0 }");
        ps.AppendLine("  Write-Host $_.Exception.Message");
        ps.AppendLine("  exit 1");
        ps.AppendLine("}");

        File.WriteAllText(runnerPath, ps.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string BuildKimiBatchInvocation(string kimiPath, string[] args)
    {
        var parts = new[] { CodexProcessManager.QuoteForCmd(kimiPath) }.Concat(args.Select(CodexProcessManager.QuoteForCmd));
        var prefix = kimiPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || kimiPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
            ? "call "
            : string.Empty;
        return prefix + string.Join(" ", parts);
    }

    private static string BuildKimiLoginCommand(string kimiPath, string kimiHome)
    {
        var kimiCommand = BuildKimiBatchInvocation(kimiPath, new[] { "login" });
        return
            $"set \"KIMI_CODE_HOME={CodexProcessManager.EscapeBatchValue(kimiHome)}\" && " +
            $"set \"KIMI_SHARE_DIR={CodexProcessManager.EscapeBatchValue(kimiHome)}\" && " +
            kimiCommand;
    }

    public static string[] BuildKimiLaunchArgs(Persona persona, string workspacePath, string agentFilePath, bool validatePaths = true)
    {
        var args = new List<string>();
        var model = persona.Model?.Trim();
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("Selected Kimi profile must define a model.");

        EnsureKimiLaunchReady(persona, workspacePath, agentFilePath, validatePaths);

        args.Add("--model");
        args.Add(model);

        var options = persona.KimiOptions;
        switch (NormalizeThinkingMode(options.ThinkingMode))
        {
            case "thinking":
                args.Add("--thinking");
                break;
            case "no-thinking":
                args.Add("--no-thinking");
                break;
        }

        if (options.PlanMode)
            args.Add("--plan");

        foreach (var dir in options.SkillsDirs.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            args.Add("--skills-dir");
            args.Add(ValidateKimiDirectory(dir, "skills directory", validatePaths));
        }

        if (!string.IsNullOrWhiteSpace(options.McpConfigFile))
        {
            args.Add("--mcp-config-file");
            args.Add(ValidateKimiFile(options.McpConfigFile, "MCP config file", validatePaths));
        }

        foreach (var dir in options.AdditionalDirs.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            args.Add("--add-dir");
            args.Add(ValidateKimiDirectory(dir, "additional workspace directory", validatePaths));
        }

        args.Add("--work-dir");
        args.Add(workspacePath);
        args.Add("--agent-file");
        args.Add(agentFilePath);
        return args.ToArray();
    }

    private static void AppendKimiOptionSummary(StringBuilder sb, KimiProfileOptions options)
    {
        var mode = NormalizeThinkingMode(options.ThinkingMode);
        if (!string.Equals(mode, "default", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"Thinking mode: {mode}");
        if (options.PlanMode)
            sb.AppendLine("Plan mode: enabled");
        if (options.SkillsDirs.Count > 0)
            sb.AppendLine("Skills dirs: " + string.Join(", ", options.SkillsDirs));
        if (!string.IsNullOrWhiteSpace(options.McpConfigFile))
            sb.AppendLine($"MCP config file: {options.McpConfigFile}");
        if (options.AdditionalDirs.Count > 0)
            sb.AppendLine("Additional workspace dirs: " + string.Join(", ", options.AdditionalDirs));
    }

    private static void EnsureKimiLaunchReady(Persona persona, string workspacePath, string agentFilePath, bool validateWorkspace)
    {
        if (validateWorkspace && !Directory.Exists(workspacePath))
            throw new DirectoryNotFoundException($"Workspace path does not exist: {workspacePath}");

        if (string.IsNullOrWhiteSpace(agentFilePath))
            throw new InvalidOperationException("Kimi agent file path is required.");
    }

    private static KimiPersonaMigrationResult NormalizeKimiPersonaOrThrow(Persona persona)
    {
        var migration = KimiPersonaMigration.Normalize(persona);
        if (migration.HasBlockingIssues)
            throw new InvalidOperationException(migration.BuildBlockingMessage());

        return migration;
    }

    private static void AppendKimiMigrationNotes(StringBuilder sb, KimiPersonaMigrationResult migration)
    {
        if (migration.Warnings.Count == 0)
            return;

        sb.AppendLine();
        sb.AppendLine("Migration notes:");
        foreach (var warning in migration.Warnings.Distinct(StringComparer.OrdinalIgnoreCase))
            sb.AppendLine("- " + warning);
    }

    private static string NormalizeThinkingMode(string? mode)
    {
        var normalized = string.IsNullOrWhiteSpace(mode) ? "default" : mode.Trim().ToLowerInvariant();
        return normalized is "default" or "thinking" or "no-thinking"
            ? normalized
            : throw new InvalidOperationException($"Invalid Kimi thinking mode '{mode}'. Allowed values: default, thinking, no-thinking.");
    }

    private static string ValidateKimiDirectory(string? path, string label, bool validatePaths)
    {
        var trimmed = path?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException($"Kimi {label} path is required.");
        if (validatePaths && !Directory.Exists(trimmed))
            throw new DirectoryNotFoundException($"Kimi {label} does not exist or is not a directory: {trimmed}");
        return trimmed;
    }

    private static string ValidateKimiFile(string? path, string label, bool validatePaths)
    {
        var trimmed = path?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException($"Kimi {label} path is required.");
        if (validatePaths && !File.Exists(trimmed))
            throw new FileNotFoundException($"Kimi {label} does not exist: {trimmed}", trimmed);
        return trimmed;
    }

    private string ResolveKimiCliExecutableOrThrow(AppSettings settings)
    {
        if (TryResolveKimiCliExecutable(settings, out var path) && !string.IsNullOrWhiteSpace(path))
            return path;

        var configured = settings.KimiCliPath?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
            throw new FileNotFoundException($"Kimi CLI executable was not found at '{configured}'. Update the Kimi CLI path in Settings or install Kimi Code CLI so 'kimi' is available on PATH.");

        throw new FileNotFoundException("Kimi CLI executable was not found. Set the Kimi CLI path in Settings or install Kimi Code CLI so 'kimi' is available on PATH.");
    }

    private bool TryResolveKimiCliExecutable(AppSettings settings, out string? path)
    {
        path = null;

        var configured = settings.KimiCliPath?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (File.Exists(configured))
            {
                path = configured;
                return true;
            }

            if (ContainsDirectorySeparator(configured))
                return false;

            if (TryWhereExecutable(configured, out path))
                return true;

            path = configured;
            _log.Warn($"Using configured Kimi command name '{configured}' without file verification.");
            return true;
        }

        foreach (var candidate in GetKimiCliCandidates())
        {
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }
        }

        if (TryWhereExecutable("kimi", out path))
            return true;

        return false;
    }

    private static bool ContainsDirectorySeparator(string value) =>
        value.IndexOf(Path.DirectorySeparatorChar) >= 0 || value.IndexOf(Path.AltDirectorySeparatorChar) >= 0;

    private bool TryWhereExecutable(string commandName, out string? path)
    {
        path = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add(commandName);

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            if (!proc.WaitForExit(3000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return false;
            }

            var output = proc.StandardOutput.ReadToEnd().Trim();
            if (string.IsNullOrWhiteSpace(output))
                return false;

            path = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            return !string.IsNullOrWhiteSpace(path);
        }
        catch (Exception ex)
        {
            _log.Warn($"Kimi CLI detection via where.exe failed for '{commandName}': {ex.Message}");
            return false;
        }
    }

    private static IEnumerable<string> GetKimiCliCandidates()
    {
        var candidates = new List<string>();
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var trimmed = dir.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            candidates.Add(Path.Combine(trimmed, "kimi.exe"));
            candidates.Add(Path.Combine(trimmed, "kimi.cmd"));
            candidates.Add(Path.Combine(trimmed, "kimi.bat"));
            candidates.Add(Path.Combine(trimmed, "kimi"));
        }

        var npmGlobal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm");
        candidates.Add(Path.Combine(npmGlobal, "kimi.cmd"));
        candidates.Add(Path.Combine(npmGlobal, "kimi.exe"));
        candidates.Add(Path.Combine(npmGlobal, "kimi.bat"));

        var localBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
        candidates.Add(Path.Combine(localBin, "kimi.exe"));
        candidates.Add(Path.Combine(localBin, "kimi.cmd"));
        candidates.Add(Path.Combine(localBin, "kimi.bat"));

        return candidates.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private AppSettings LoadSettings() => _config.LoadList<AppSettings>("settings").FirstOrDefault() ?? new AppSettings();

    private string? ResolveRoleTemplatePath(string? agentsTemplatePath)
    {
        if (string.IsNullOrWhiteSpace(agentsTemplatePath))
            return null;

        var relative = agentsTemplatePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (relative.StartsWith("Templates" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            relative = relative[("Templates" + Path.DirectorySeparatorChar).Length..];

        var path = Path.Combine(_config.TemplatesDir, relative);
        return File.Exists(path) ? path : null;
    }

    private static string EscapePowerShellSingleQuoted(string value) =>
        (value ?? string.Empty).Replace("'", "''");
}
