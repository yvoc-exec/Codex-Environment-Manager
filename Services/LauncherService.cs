using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using CodexEnvironmentManager.Models;

namespace CodexEnvironmentManager.Services;

public sealed class DesktopLaunchRecoveryResult
{
    public bool JunctionRestored { get; set; }
    public bool RelaunchAttempted { get; set; }
    public bool RelaunchSucceeded { get; set; }
    public bool ManualRelaunchRequired { get; set; }
    public string Message { get; set; } = "";
}

public class LauncherService
{
    private static readonly Mutex LaunchMutex = new(false, @"Local\CodexEnvironmentManager_Launch");

    private readonly AccountManager _accountManager;
    private readonly PersonaEngine _personaEngine;
    private readonly SessionManager _sessionManager;
    private readonly CodexProcessManager _processManager;
    private readonly DesktopWorkspaceLauncher _desktopWorkspaceLauncher;
    private readonly GitStateGuard _gitGuard;
    private readonly KimiCliManager _kimiCliManager;
    private readonly LogService _log;
    private readonly ConfigService _config;
    private bool _launchInProgress;

    public LauncherService(AccountManager am, PersonaEngine pe, SessionManager sm, CodexProcessManager pm, DesktopWorkspaceLauncher desktopWorkspaceLauncher, GitStateGuard gg, KimiCliManager kimiCliManager, LogService log, ConfigService config)
    {
        _accountManager = am;
        _personaEngine = pe;
        _sessionManager = sm;
        _processManager = pm;
        _desktopWorkspaceLauncher = desktopWorkspaceLauncher;
        _gitGuard = gg;
        _kimiCliManager = kimiCliManager;
        _log = log;
        _config = config;
    }

    public bool IsLaunchInProgress => _launchInProgress;

    public string BuildLaunchPreview(Account acct, Persona persona, Workspace ws, string launchType)
    {
        ValidateLaunchInputs(acct, persona, ws);
        var accountPath = JunctionManager.GetAccountProfilePath(acct.Id);
        var profileName = PersonaEngine.GetProfileName(persona);
        var args = BuildCodexArgs(profileName, persona, ws);
        var model = GetConfigValue(persona, "model", "<Codex default>");
        var reasoning = GetConfigValue(persona, "model_reasoning_effort", "<Codex default>");
        var sandbox = GetConfigValue(persona, "sandbox_mode", "<Codex default>");
        var approval = GetConfigValue(persona, "approval_policy", "<Codex default>");
        var approvalsReviewer = GetApprovalsReviewer(persona);

        var sb = new StringBuilder();
        sb.AppendLine($"Launch type: {launchType}");
        sb.AppendLine();
        sb.AppendLine($"Account: {acct.Name} ({acct.Id})");
        sb.AppendLine($"CODEX_HOME: {accountPath}");
        sb.AppendLine();
        sb.AppendLine($"CEM profile/persona: {persona.Name}");
        sb.AppendLine($"Codex profile: {profileName}");
        sb.AppendLine($"Model: {model}");
        sb.AppendLine($"Reasoning: {reasoning}");
        sb.AppendLine($"Sandbox: {sandbox}");
        sb.AppendLine($"Approval: {approval}");
        sb.AppendLine($"Approvals reviewer: {approvalsReviewer}");
        sb.AppendLine();
        sb.AppendLine($"Workspace: {ws.Name}");
        sb.AppendLine(ws.Path);
        sb.AppendLine();
        sb.AppendLine("CLI contract:");
        sb.AppendLine("codex " + string.Join(" ", args.Select(CodexProcessManager.QuoteForCmd)));
        sb.AppendLine();
        sb.AppendLine("Behavioral role catalog is written to:");
        sb.AppendLine(Path.Combine(accountPath, "AGENTS.md"));
        sb.AppendLine("Active role selector is injected by the selected Codex profile.");
        sb.AppendLine();
        var settings = LoadSettings();
        sb.AppendLine("Active context is written to:");
        sb.AppendLine(Path.Combine(accountPath, "CEM_ACTIVE_CONTEXT.json"));
        sb.AppendLine();
        sb.AppendLine("Account runtime config on launch:");
        sb.AppendLine($"[windows] sandbox = {settings.WindowsSandboxMode}");
        sb.AppendLine(settings.TrustWorkspaceOnLaunch
            ? "[projects.<selected workspace>] trust_level = trusted"
            : "Workspace auto-trust disabled");
        return sb.ToString();
    }

    public string LaunchDesktop(Account acct, Persona persona, Workspace ws)
    {
        var launchStatus = "Desktop launched.";
        RunUnderLaunchLock(() =>
        {
            _log.Info($"LaunchDesktop: account={acct.Name} ({acct.Id}), persona={persona.Name} ({persona.Id}), workspace={ws.Name} ({ws.Id}) path={ws.Path}");
            ValidateLaunchInputs(acct, persona, ws);
            if (IsGitGuardEnabled()) _gitGuard.Check(ws.Path);
            var accountPath = JunctionManager.GetAccountProfilePath(acct.Id);
            var profileName = PersonaEngine.GetProfileName(persona);
            var unmanagedDesktopReason = GetUnmanagedDesktopLaunchBlockReason(_sessionManager.Active, _processManager.GetLiveDesktopTargets(), _sessionManager.InspectSession);
            if (!string.IsNullOrWhiteSpace(unmanagedDesktopReason))
                throw new InvalidOperationException(unmanagedDesktopReason);
            var previousActiveAccountId = JunctionManager.LoadActiveAccount();
            var previousDesktopSession = CaptureRecoverableDesktopSession(previousActiveAccountId);
            var preparation = PrepareDesktopLaunch(acct, persona, ws, accountPath, profileName);
            var junctionSwapped = false;
            var killSequenceCompleted = false;
            try
            {
                var killResults = _sessionManager.KillAllDesktop();
                var failedKills = killResults.Where(r => !r.KillConfirmed).ToList();
                if (failedKills.Count > 0)
                {
                    var details = string.Join(Environment.NewLine, failedKills.Select(r => $"- {r.Message}"));
                    throw new InvalidOperationException("Unable to verify termination of one or more existing Desktop sessions:" + Environment.NewLine + details);
                }
                killSequenceCompleted = true;

                JunctionManager.SwapToAccount(acct.Id, _log);
                junctionSwapped = true;

                var proc = _desktopWorkspaceLauncher.StartBaseLaunchAndQueueWorkspaceBinding(preparation.LaunchPlan, preparation.Psi);
                RegisterDesktopSession(acct, persona, ws, preparation.LaunchPlan, proc);
            }
            catch (Exception ex)
            {
                throw RecoverDesktopLaunchAfterFailure(previousActiveAccountId, acct.Id, previousDesktopSession, killSequenceCompleted, junctionSwapped, ex);
            }
        });
        return launchStatus;
    }

    public void LaunchCliCompanion(Account acct, Persona persona, Workspace ws)
    {
        RunUnderLaunchLock(() =>
        {
            _log.Info($"LaunchCliCompanion: account={acct.Name}, persona={persona.Name}, workspace={ws.Name}");

            if (!CodexProcessManager.TryFindCodexCliExecutable(out var codexPath) || string.IsNullOrWhiteSpace(codexPath))
            {
                _log.Error("Codex CLI not found in PATH");
                throw new InvalidOperationException("Codex CLI not found. Install with: npm i -g @openai/codex");
            }

            ValidateLaunchInputs(acct, persona, ws);
            if (IsGitGuardEnabled()) _gitGuard.Check(ws.Path);

            var settings = LoadSettings();
            var accountPath = JunctionManager.GetAccountProfilePath(acct.Id);
            var instructionsFile = _personaEngine.ApplyToWorkspace(ws, acct, persona, false);
            var profileName = PersonaEngine.GetProfileName(persona);

            var session = new Session
            {
                AccountId = acct.Id,
                PersonaId = persona.Id,
                WorkspaceId = ws.Id,
                Type = "cli",
                AccountProvider = acct.ResolvedProvider
            };

            var args = BuildCodexArgs(profileName, persona, ws);
            WriteActiveContext(accountPath, "cli", session.Id, acct, persona, ws, profileName, instructionsFile, codexPath, args);
            _personaEngine.ApplyToAccount(acct, persona, instructionsFile, _config.LoadList<Persona>("personas"));
            PersonaEngine.EnsureAccountRuntimeConfig(acct.Id, ws.Path, settings.WindowsSandboxMode, settings.TrustWorkspaceOnLaunch);
            PersonaEngine.ValidateAccountProfileExists(acct.Id, profileName);
            PersonaEngine.ValidateAccountBaseConfigClean(acct.Id);
            var launcherScript = CreateCliLauncherScript(session.Id, codexPath, accountPath, ws, acct, persona, profileName, args);
            session.StartedMarkerPath = Path.Combine(JunctionManager.SwitcherDir, "sessions", session.Id, "started.txt");
            session.ExitMarkerPath = Path.Combine(JunctionManager.SwitcherDir, "sessions", session.Id, "exit.txt");
            session.StopMarkerPath = Path.Combine(JunctionManager.SwitcherDir, "sessions", session.Id, "stop.txt");
            session.LauncherScriptPath = launcherScript;
            session.KillMarker = "CEM_SESSION_" + session.Id;
            session.TerminalWindowName = "CodexEnvironmentManager";
            session.CodexPidPath = Path.Combine(JunctionManager.SwitcherDir, "sessions", session.Id, "codex-wrapper.pid");
            WriteLaunchPlan("cli", session.Id, acct, persona, ws, accountPath, profileName, instructionsFile, codexPath, args);
            ProcessStartInfo psi;
            var wtPath = settings.PreferWindowsTerminalForCli ? CodexProcessManager.FindWindowsTerminal() : null;

            if (wtPath != null)
            {
                psi = new ProcessStartInfo
                {
                    FileName = wtPath,
                    UseShellExecute = false,
                    WorkingDirectory = ws.Path
                };
                psi.ArgumentList.Add("-w");
                psi.ArgumentList.Add(session.TerminalWindowName);
                psi.ArgumentList.Add("new-tab");
                psi.ArgumentList.Add("--title");
                psi.ArgumentList.Add($"CEM {session.Id[..8]} [{persona.Name}] - {ws.Name}");
                psi.ArgumentList.Add("cmd.exe");
                // In Windows Terminal tab mode, use /c instead of /k so the selected tab can close
                // when launch.cmd exits after a CEM kill/stop marker.
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add($"set CEM_SESSION_MARKER={session.KillMarker} && call \"{launcherScript}\"");
                _log.Warn("Windows Terminal CLI mode targets a shared named window, uses /c for close-on-exit behavior, and uses a unique per-session command marker for kill.");
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    WorkingDirectory = ws.Path
                };
                psi.ArgumentList.Add("/k");
                psi.ArgumentList.Add($"set CEM_SESSION_MARKER={session.KillMarker} && call \"{launcherScript}\"");
            }

            ApplyEnvironment(psi, acct, persona, accountPath, includeApiKeyFallback: acct.Type == "api_key");

            _log.Info($"Spawning CLI companion through launcher script: {launcherScript}");
            var proc = Process.Start(psi);

            // Always register CLI sessions after Process.Start returns without throwing.
            // In Windows Terminal mode, wt.exe may hand the tab off to an existing window and exit immediately,
            // or Process.Start may not give us a useful long-lived PID. The authoritative identity is the
            // CEM session marker + launcher script path.
            session.ProcessId = wtPath != null ? null : proc?.Id;
            _sessionManager.Register(session);

            if (proc != null)
            {
                _log.Info(wtPath != null
                    ? $"CLI companion launched in Windows Terminal; tracking session {session.Id} by marker/script only (wt PID {proc.Id})."
                    : $"CLI companion launcher PID {proc.Id}, session {session.Id}");
            }
            else
            {
                _log.Warn($"CLI companion Process.Start returned null, but session {session.Id} was registered for marker/script tracking.");
            }
        });
    }

    public string BuildKimiLaunchPreview(Account acct, Persona? persona, Workspace ws) =>
        _kimiCliManager.BuildLaunchPreview(acct, persona, ws);

    public void LaunchKimiCompanion(Account acct, Persona? persona, Workspace ws)
    {
        RunUnderLaunchLock(() =>
        {
            _log.Info($"LaunchKimiCompanion: account={acct.Name} ({acct.Id}), workspace={ws.Name} ({ws.Id}) path={ws.Path}, persona={(persona?.Name ?? "(none)")}");

            if (persona == null)
                throw new InvalidOperationException("Selected profile is invalid.");

            ValidateKimiLaunchInputs(acct, persona, ws);
            if (IsGitGuardEnabled()) _gitGuard.Check(ws.Path);

            var settings = LoadSettings();
            var session = new Session
            {
                AccountId = acct.Id,
                WorkspaceId = ws.Id,
                Type = "kimi-cli",
                AccountProvider = acct.ResolvedProvider
            };

            var setup = _kimiCliManager.PrepareLaunch(session.Id, acct, persona, ws, settings.PreferWindowsTerminalForCli);
            session.StartedMarkerPath = Path.Combine(setup.SessionDirectory, "started.txt");
            session.ExitMarkerPath = Path.Combine(setup.SessionDirectory, "exit.txt");
            session.StopMarkerPath = Path.Combine(setup.SessionDirectory, "stop.txt");
            session.LauncherScriptPath = setup.LaunchScriptPath;
            session.KillMarker = "CEM_KIMI_SESSION_" + session.Id;
            session.TerminalWindowName = "CodexEnvironmentManager";

            WriteKimiLaunchPlan(session.Id, acct, persona, ws, setup);

            _log.Info($"Spawning Kimi CLI through launcher script: {setup.LaunchScriptPath}");
            var proc = Process.Start(setup.StartInfo);

            session.ProcessId = setup.UsesWindowsTerminal ? null : proc?.Id;
            _sessionManager.Register(session);

            if (proc != null)
            {
                _log.Info(setup.UsesWindowsTerminal
                    ? $"Kimi CLI launched in Windows Terminal; tracking session {session.Id} by marker/script only (wt PID {proc.Id})."
                    : $"Kimi CLI launcher PID {proc.Id}, session {session.Id}");
            }
            else
            {
                _log.Warn($"Kimi CLI Process.Start returned null, but session {session.Id} was registered for marker/script tracking.");
            }
        });
    }

    private void RunUnderLaunchLock(Action action)
    {
        if (_launchInProgress)
        {
            _log.Warn("Launch blocked: another launch is in progress");
            throw new InvalidOperationException("Another launch is currently in progress. Please wait.");
        }

        var lockTaken = false;
        try
        {
            lockTaken = LaunchMutex.WaitOne(TimeSpan.FromSeconds(20));
            if (!lockTaken) throw new TimeoutException("Timed out waiting for launch lock.");
            _launchInProgress = true;
            action();
        }
        finally
        {
            _launchInProgress = false;
            if (lockTaken) LaunchMutex.ReleaseMutex();
        }
    }

    private void ValidateLaunchInputs(Account acct, Persona persona, Workspace ws)
    {
        if (string.IsNullOrWhiteSpace(acct.Id))
            throw new InvalidOperationException("Selected account is invalid.");
        if (string.IsNullOrWhiteSpace(persona.Id))
            throw new InvalidOperationException("Selected persona is invalid.");
        if (string.IsNullOrWhiteSpace(ws.Path) || !Directory.Exists(ws.Path))
            throw new DirectoryNotFoundException($"Workspace path does not exist: {ws.Path}");

        var accountPath = JunctionManager.GetAccountProfilePath(acct.Id);
        if (!Directory.Exists(accountPath))
            throw new DirectoryNotFoundException($"Account profile path does not exist: {accountPath}");

        if (!Directory.Exists(ws.Path))
            throw new DirectoryNotFoundException($"Workspace path does not exist: {ws.Path}");

        foreach (var key in persona.ConfigOverrides.Keys)
        {
            if (!IsSafeConfigKey(key))
                throw new InvalidOperationException($"Unsafe Codex config key in persona '{persona.Name}': {key}");
        }

        ValidateProviderCompatibility(acct, persona);
    }

    private void ValidateKimiLaunchInputs(Account acct, Persona persona, Workspace ws)
    {
        if (string.IsNullOrWhiteSpace(ws.Path) || !Directory.Exists(ws.Path))
            throw new DirectoryNotFoundException($"Workspace path does not exist: {ws.Path}");

        ValidateProviderCompatibility(acct, persona);
    }

    private static void ValidateProviderCompatibility(Account acct, Persona persona)
    {
        var accountProvider = ProviderCapabilities.ForProvider(acct.ResolvedProvider).ProviderId;
        var personaProvider = ProviderCapabilities.ForModel(persona.Model).ProviderId;

        if (!string.Equals(accountProvider, personaProvider, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Provider mismatch: account '{acct.Name}' is {accountProvider}, but profile '{persona.Name}' is {personaProvider}. " +
                "Select a matching account and profile before launching.");
        }
    }

    private bool IsGitGuardEnabled() => LoadSettings().GitGuardEnabled;

    private AppSettings LoadSettings() => _config.LoadList<AppSettings>("settings").FirstOrDefault() ?? new AppSettings();

    private static bool IsSafeConfigKey(string key) =>
        !string.IsNullOrWhiteSpace(key) && key.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.');

    private static string GetConfigValue(Persona persona, string key, string fallback) =>
        persona.ConfigOverrides.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static string GetApprovalsReviewer(Persona persona) =>
        string.Equals(persona.ApprovalsReviewer?.Trim(), "auto_review", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(persona.ApprovalsReviewer?.Trim(), "guardian_subagent", StringComparison.OrdinalIgnoreCase)
            ? "auto_review"
            : "user";

    private static Dictionary<string, string> GetEffectiveProfileValues(Persona persona)
    {
        var values = new Dictionary<string, string>(persona.ConfigOverrides, StringComparer.OrdinalIgnoreCase);
        if (!values.ContainsKey("model")) values["model"] = "<Codex default>";
        if (!values.ContainsKey("model_reasoning_effort")) values["model_reasoning_effort"] = "<Codex default>";
        if (!values.ContainsKey("sandbox_mode")) values["sandbox_mode"] = "<Codex default>";
        if (!values.ContainsKey("approval_policy")) values["approval_policy"] = "<Codex default>";
        if (!values.ContainsKey("approvals_reviewer")) values["approvals_reviewer"] = GetApprovalsReviewer(persona);
        return values;
    }

    private void ApplyEnvironment(ProcessStartInfo psi, Account acct, Persona persona, string accountPath, bool includeApiKeyFallback)
    {
        psi.EnvironmentVariables["CODEX_HOME"] = accountPath;

        if (includeApiKeyFallback && acct.Type == "api_key")
        {
            var key = _accountManager.DecryptApiKey(acct);
            if (!string.IsNullOrWhiteSpace(key))
            {
                psi.EnvironmentVariables["OPENAI_API_KEY"] = key;
                psi.EnvironmentVariables["CODEX_API_KEY"] = key;
                _log.Warn("Injected API key fallback into child environment; prefer login bootstrap for normal use.");
            }
        }

        foreach (var kv in persona.EnvVars)
        {
            if (IsSafeEnvironmentVariableName(kv.Key))
                psi.EnvironmentVariables[kv.Key] = kv.Value;
            else
                _log.Warn($"Skipped unsafe environment variable name: {kv.Key}");
        }
    }

    private string CreateCliLauncherScript(string sessionId, string codexPath, string accountPath, Workspace ws, Account acct, Persona persona, string profileName, string[] args)
    {
        var dir = Path.Combine(JunctionManager.SwitcherDir, "sessions", sessionId);
        Directory.CreateDirectory(dir);

        var scriptPath = Path.Combine(dir, "launch.cmd");
        var runnerPath = Path.Combine(dir, "run_codex_wrapper.ps1");
        var exitMarker = Path.Combine(dir, "exit.txt");
        var startedMarker = Path.Combine(dir, "started.txt");
        var stopMarker = Path.Combine(dir, "stop.txt");
        var pidPath = Path.Combine(dir, "codex-wrapper.pid");

        WriteCodexPowerShellWrapper(runnerPath, codexPath, args, pidPath, stopMarker);

        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("setlocal");
        sb.AppendLine($"title Codex [{EscapeBatchTitle(persona.Name)}] - {EscapeBatchTitle(ws.Name)}");
        sb.AppendLine($"cd /d \"{CodexProcessManager.EscapeBatchValue(ws.Path)}\"");
        sb.AppendLine("if errorlevel 1 exit /b 1");
        sb.AppendLine($"set \"CODEX_HOME={CodexProcessManager.EscapeBatchValue(accountPath)}\"");
        sb.AppendLine($"set \"CODEX_ENV_MANAGER_SESSION_ID={CodexProcessManager.EscapeBatchValue(sessionId)}\"");
        sb.AppendLine($"set \"CEM_SESSION_MARKER=CEM_SESSION_{CodexProcessManager.EscapeBatchValue(sessionId)}\"");
        sb.AppendLine($"set \"CEM_EXPECTED_ACCOUNT_ID={CodexProcessManager.EscapeBatchValue(acct.Id)}\"");
        sb.AppendLine($"set \"CEM_EXPECTED_ACCOUNT_NAME={CodexProcessManager.EscapeBatchValue(acct.Name)}\"");
        sb.AppendLine($"set \"CEM_EXPECTED_PERSONA_ID={CodexProcessManager.EscapeBatchValue(persona.Id)}\"");
        sb.AppendLine($"set \"CEM_EXPECTED_PERSONA_NAME={CodexProcessManager.EscapeBatchValue(persona.Name)}\"");
        sb.AppendLine($"set \"CEM_EXPECTED_PROFILE={CodexProcessManager.EscapeBatchValue(profileName)}\"");
        sb.AppendLine($"set \"CEM_EXPECTED_WORKSPACE_ID={CodexProcessManager.EscapeBatchValue(ws.Id)}\"");
        sb.AppendLine($"set \"CEM_EXPECTED_WORKSPACE_NAME={CodexProcessManager.EscapeBatchValue(ws.Name)}\"");
        sb.AppendLine($"set \"CEM_EXPECTED_WORKSPACE_PATH={CodexProcessManager.EscapeBatchValue(ws.Path)}\"");
        foreach (var kv in persona.EnvVars)
        {
            if (IsSafeEnvironmentVariableName(kv.Key))
                sb.AppendLine($"set \"{kv.Key}={CodexProcessManager.EscapeBatchValue(kv.Value)}\"");
        }
        sb.AppendLine("echo [CEM] Expected account  : %CEM_EXPECTED_ACCOUNT_NAME% (%CEM_EXPECTED_ACCOUNT_ID%)");
        sb.AppendLine("echo [CEM] Expected CODEX_HOME: %CODEX_HOME%");
        sb.AppendLine("echo [CEM] Expected profile  : %CEM_EXPECTED_PERSONA_NAME% (%CEM_EXPECTED_PROFILE%)");
        sb.AppendLine("echo [CEM] Expected workspace: %CEM_EXPECTED_WORKSPACE_PATH%");
        sb.AppendLine("echo [CEM] Actual directory  : %CD%");
        sb.AppendLine("echo [CEM] Active context   : %CODEX_HOME%\\CEM_ACTIVE_CONTEXT.json");
        sb.AppendLine("echo [CEM] Launch command   : codex --cd \"%CEM_EXPECTED_WORKSPACE_PATH%\" --profile %CEM_EXPECTED_PROFILE%");
        sb.AppendLine($@"echo started > ""{CodexProcessManager.EscapeBatchValue(startedMarker)}""");
        sb.AppendLine("echo.");
        sb.AppendLine($@"powershell.exe -NoProfile -ExecutionPolicy Bypass -File ""{CodexProcessManager.EscapeBatchValue(runnerPath)}""");
        sb.AppendLine("set CODEX_EXIT_CODE=%ERRORLEVEL%");
        sb.AppendLine($"if exist \"{CodexProcessManager.EscapeBatchValue(stopMarker)}\" (");
        sb.AppendLine($@"  echo killed > ""{CodexProcessManager.EscapeBatchValue(exitMarker)}""");
        sb.AppendLine("  echo.");
        sb.AppendLine("  echo [CEM] Stop marker detected. Closing this CLI session.");
        sb.AppendLine("  endlocal");
        sb.AppendLine("  exit 0");
        sb.AppendLine(")");
        sb.AppendLine($@"echo %CODEX_EXIT_CODE% > ""{CodexProcessManager.EscapeBatchValue(exitMarker)}""");
        sb.AppendLine("echo.");
        sb.AppendLine("echo Codex exited with code %CODEX_EXIT_CODE%.");
        sb.AppendLine("endlocal");
        sb.AppendLine("exit /b %CODEX_EXIT_CODE%");

        File.WriteAllText(scriptPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return scriptPath;
    }

    private static void WriteCodexPowerShellWrapper(string runnerPath, string codexPath, string[] args, string pidPath, string stopMarker)
    {
        var cmdLine = "call " + CodexProcessManager.QuoteForCmd(codexPath) + " " + string.Join(" ", args.Select(CodexProcessManager.QuoteForCmd));

        var ps = new StringBuilder();
        ps.AppendLine("$ErrorActionPreference = 'Stop'");
        ps.AppendLine("$pidPath = '" + EscapePowerShellSingleQuoted(pidPath) + "'");
        ps.AppendLine("$stopMarker = '" + EscapePowerShellSingleQuoted(stopMarker) + "'");
        ps.AppendLine("$cmdLine = '" + EscapePowerShellSingleQuoted(cmdLine) + "'");
        ps.AppendLine("try {");
        ps.AppendLine("  $p = Start-Process -FilePath $env:ComSpec -ArgumentList @('/d','/s','/c', $cmdLine) -NoNewWindow -PassThru");
        ps.AppendLine("  Set-Content -LiteralPath $pidPath -Value $p.Id -Encoding ASCII");
        ps.AppendLine("  $p.WaitForExit()");
        ps.AppendLine("  if (Test-Path -LiteralPath $stopMarker) { exit 0 }");
        ps.AppendLine("  exit $p.ExitCode");
        ps.AppendLine("} catch {");
        ps.AppendLine("  if (Test-Path -LiteralPath $stopMarker) { exit 0 }");
        ps.AppendLine("  Write-Host $_.Exception.Message");
        ps.AppendLine("  exit 1");
        ps.AppendLine("}");

        File.WriteAllText(runnerPath, ps.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapePowerShellSingleQuoted(string value) =>
        (value ?? string.Empty).Replace("'", "''");

    private void WriteActiveContext(string accountPath, string launchType, string? sessionId, Account acct, Persona persona, Workspace ws, string profileName, string? instructionsFile, string? codexPath, string[]? codexArgs, string? commandPreviewOverride = null)
    {
        var context = BuildLaunchContext(launchType, sessionId, acct, persona, ws, accountPath, profileName, instructionsFile, codexPath, codexArgs, commandPreviewOverride);
        var json = JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(accountPath, "CEM_ACTIVE_CONTEXT.json"), json, Encoding.UTF8);
    }

    private object BuildLaunchContext(string launchType, string? sessionId, Account acct, Persona persona, Workspace ws, string accountPath, string profileName, string? instructionsFile, string? codexPath, string[]? codexArgs, string? commandPreviewOverride = null)
    {
        return new
        {
            GeneratedAt = DateTime.Now.ToString("O"),
            LaunchType = launchType,
            SessionId = sessionId,
            ExpectedAccount = new { acct.Id, acct.Name, acct.Type, CodexHome = accountPath },
            ExpectedProfile = new
            {
                persona.Id,
                persona.Name,
                Profile = profileName,
                ConfigOverrides = persona.ConfigOverrides,
                EffectiveProfileValues = GetEffectiveProfileValues(persona)
            },
            ExpectedWorkspace = new { ws.Id, ws.Name, ws.Path },
            InstructionsFile = instructionsFile,
            CodexPath = codexPath,
            CodexArgs = codexArgs,
            CommandPreview = commandPreviewOverride ?? (codexPath == null || codexArgs == null ? null : CodexProcessManager.QuoteForCmd(codexPath) + " " + string.Join(" ", codexArgs.Select(CodexProcessManager.QuoteForCmd))),
            Verification = new
            {
                Contract = "CEM expects Codex to use this CODEX_HOME, Codex profile, and workspace. Verify with the visible Codex model/directory banner and by reading CEM_ACTIVE_CONTEXT.json.",
                ProfileControls = "model, reasoning effort, sandbox mode, approval policy, approvals_reviewer, and optional model_instructions_file",
                BehaviorControls = "CODEX_HOME/AGENTS.md role catalog plus profile-scoped developer_instructions"
            }
        };
    }

    private void WriteLaunchPlan(string launchType, string? sessionId, Account acct, Persona persona, Workspace ws, string accountPath, string profileName, string? instructionsFile, string? codexPath, string[]? codexArgs, string? commandPreviewOverride = null)
    {
        var sessionsDir = Path.Combine(JunctionManager.SwitcherDir, "sessions");
        Directory.CreateDirectory(sessionsDir);

        var plan = BuildLaunchContext(launchType, sessionId, acct, persona, ws, accountPath, profileName, instructionsFile, codexPath, codexArgs, commandPreviewOverride);

        var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(JunctionManager.SwitcherDir, "last_launch_plan.json"), json, Encoding.UTF8);

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var sessionDir = Path.Combine(sessionsDir, sessionId);
            Directory.CreateDirectory(sessionDir);
            File.WriteAllText(Path.Combine(sessionDir, "launch_plan.json"), json, Encoding.UTF8);
        }
    }

    private void WriteKimiLaunchPlan(string sessionId, Account acct, Persona? persona, Workspace ws, KimiLaunchSetup setup)
    {
        var plan = new
        {
            GeneratedAt = DateTime.Now.ToString("O"),
            LaunchType = "kimi-cli",
            SessionId = sessionId,
            Account = new { acct.Id, acct.Name, acct.Provider, acct.Type },
            KimiCodeHome = KimiCliManager.GetKimiCodeHome(acct),
            SelectedProfile = persona?.Name,
            SelectedModel = persona?.Model,
            Workspace = new { ws.Id, ws.Name, ws.Path },
            KimiExecutablePath = setup.KimiExecutablePath,
            AgentFilePath = setup.AgentFilePath,
            PromptFilePath = setup.PromptFilePath,
            RoleTemplatePath = setup.RoleTemplatePath,
            LaunchScriptPath = setup.LaunchScriptPath,
            KillMarker = "CEM_KIMI_SESSION_" + sessionId,
            ExtraArgs = setup.ExtraArgs,
            LaunchArgs = setup.LaunchArgs,
            KimiOptions = setup.KimiOptions,
            CommandPreview = "kimi " + string.Join(" ", setup.LaunchArgs
                .Select(CodexProcessManager.QuoteForCmd)),
            RuntimeNotes = new
            {
                KimiAuth = "Managed by Kimi login/setup; CEM does not copy or store credentials.",
                ArtifactLocation = setup.SessionDirectory
            }
        };

        var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(setup.SessionDirectory, "launch_plan.json"), json, Encoding.UTF8);
        File.WriteAllText(Path.Combine(JunctionManager.SwitcherDir, "last_kimi_launch_plan.json"), json, Encoding.UTF8);
    }

    private static string[] BuildCodexArgs(string profileName, Persona persona, Workspace ws)
    {
        // Profile is the source of truth for model/reasoning/sandbox/approval.
        // Keep only non-profile extra args so old default CliArgs cannot silently override the selected profile.
        return new[] { "--cd", ws.Path, "--profile", profileName }
            .Concat(FilterNonProfileCliArgs(persona.CliArgs))
            .ToArray();
    }

    private static IEnumerable<string> FilterNonProfileCliArgs(IEnumerable<string> args)
    {
        var skipNextFor = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--sandbox", "--ask-for-approval", "--model", "-m", "--profile", "-p", "--cd"
        };

        var list = args?.ToList() ?? new List<string>();
        for (var i = 0; i < list.Count; i++)
        {
            var arg = list[i];
            if (string.IsNullOrWhiteSpace(arg)) continue;

            var key = arg.Contains('=') ? arg[..arg.IndexOf('=')] : arg;
            if (skipNextFor.Contains(key) || string.Equals(key, "-C", StringComparison.Ordinal))
            {
                if (!arg.Contains('=') && i + 1 < list.Count) i++;
                continue;
            }

            // Do not allow -c overrides for profile-controlled keys from the legacy persona field.
            if (string.Equals(key, "-c", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "--config", StringComparison.OrdinalIgnoreCase))
            {
                var value = arg.Contains('=') ? arg[(arg.IndexOf('=') + 1)..] : (i + 1 < list.Count ? list[++i] : string.Empty);
                if (IsProfileControlledConfigOverride(value)) continue;
                yield return arg;
                if (!arg.Contains('=') && !string.IsNullOrWhiteSpace(value)) yield return value;
                continue;
            }

            yield return arg;
        }
    }

    private static bool IsProfileControlledConfigOverride(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var key = value.Split('=', 2)[0].Trim();
        return string.Equals(key, "profile", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "model", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "model_reasoning_effort", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "sandbox_mode", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "approval_policy", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "approvals_reviewer", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(key, "model_instructions_file", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCodexBatchInvocation(string codexPath, string[] args)
    {
        var parts = new[] { CodexProcessManager.QuoteForCmd(codexPath) }.Concat(args.Select(CodexProcessManager.QuoteForCmd));
        var prefix = codexPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || codexPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
            ? "call "
            : string.Empty;
        return prefix + string.Join(" ", parts);
    }

    public static string EscapeBatchTitle(string value) =>
        CodexProcessManager.EscapeBatchValue(value ?? string.Empty);

    public static bool IsReliableDesktopRecoverySession(Session? session)
    {
        return session != null &&
               string.Equals(session.Type, "desktop", StringComparison.OrdinalIgnoreCase) &&
               !session.IsBestEffortUntracked &&
               !string.IsNullOrWhiteSpace(session.AccountId) &&
               !string.IsNullOrWhiteSpace(session.PersonaId) &&
               !string.IsNullOrWhiteSpace(session.WorkspaceId) &&
               session.ProcessId.HasValue;
    }

    public static DesktopLaunchRecoveryResult BuildDesktopLaunchRecoveryResult(
        bool junctionRestored,
        bool relaunchAttempted,
        bool relaunchSucceeded,
        bool manualRelaunchRequired,
        string? previousAccountName,
        string? previousWorkspaceName)
    {
        var accountLabel = string.IsNullOrWhiteSpace(previousAccountName) ? "<previous account>" : previousAccountName;
        var workspaceLabel = string.IsNullOrWhiteSpace(previousWorkspaceName) ? "<previous workspace>" : previousWorkspaceName;

        var sb = new StringBuilder();
        sb.Append("Desktop launch failed after kill/swap.");
        sb.Append(junctionRestored
            ? " Previous junction/account was restored."
            : " Previous junction/account could not be restored.");

        if (relaunchAttempted)
        {
            sb.Append(relaunchSucceeded
                ? $" Previous Desktop relaunched for {accountLabel} / {workspaceLabel}."
                : " Previous Desktop relaunch was attempted but failed.");
        }
        else if (manualRelaunchRequired)
        {
            sb.Append(" Previous Desktop relaunch was not attempted.");
        }

        sb.Append(manualRelaunchRequired
            ? " Manual Desktop relaunch is required."
            : " Manual Desktop relaunch is not required.");

        return new DesktopLaunchRecoveryResult
        {
            JunctionRestored = junctionRestored,
            RelaunchAttempted = relaunchAttempted,
            RelaunchSucceeded = relaunchSucceeded,
            ManualRelaunchRequired = manualRelaunchRequired,
            Message = sb.ToString()
        };
    }

    public static string? GetDesktopLaunchRollbackTarget(string? previousActiveAccountId, string currentAccountId)
    {
        if (string.IsNullOrWhiteSpace(previousActiveAccountId)) return null;
        if (string.Equals(previousActiveAccountId, currentAccountId, StringComparison.OrdinalIgnoreCase)) return null;
        return previousActiveAccountId;
    }

    private static string? GetUnmanagedDesktopLaunchBlockReason(IReadOnlyCollection<Session> activeSessions, IReadOnlyCollection<DesktopProcessTarget> liveDesktopTargets, Func<Session, SessionInspectionResult> inspectSession)
    {
        if (liveDesktopTargets.Count == 0)
            return null;

        var trackedLiveDesktopPids = new HashSet<int>();
        foreach (var session in activeSessions.Where(s => s.Type.StartsWith("desktop", StringComparison.OrdinalIgnoreCase)))
        {
            var inspection = inspectSession(session);
            if (inspection.State != SessionLiveState.Live || !inspection.TargetProcessId.HasValue)
                continue;

            trackedLiveDesktopPids.Add(inspection.TargetProcessId.Value);
        }

        var unmanagedTargets = liveDesktopTargets
            .Where(target => !trackedLiveDesktopPids.Contains(target.ProcessId))
            .ToList();

        if (unmanagedTargets.Count == 0)
            return null;

        var details = string.Join(", ", unmanagedTargets.Select(target => $"{target.Name} PID {target.ProcessId}"));
        return "Codex Desktop is already running outside CEM tracking. Close it first or attach/kill it manually before launching a profile-specific Desktop session. Detected unmanaged process(es): " + details + ".";
    }

    private (DesktopWorkspaceLaunchPlan LaunchPlan, ProcessStartInfo Psi, string InstructionsFile) PrepareDesktopLaunch(Account acct, Persona persona, Workspace ws, string accountPath, string profileName)
    {
        var settings = LoadSettings();
        var launchPlan = _desktopWorkspaceLauncher.BuildLaunchPlan(acct, persona, ws, accountPath, profileName);
        if (!launchPlan.CanLaunch)
            throw new InvalidOperationException(launchPlan.FailureReason ?? "No valid Desktop launch method is available.");

        var instructionsFile = _personaEngine.ApplyToWorkspace(ws, acct, persona, false);
        _personaEngine.ApplyToAccount(acct, persona, instructionsFile, _config.LoadList<Persona>("personas"));
        PersonaEngine.EnsureAccountRuntimeConfig(acct.Id, ws.Path, settings.WindowsSandboxMode, settings.TrustWorkspaceOnLaunch);
        PersonaEngine.ValidateAccountProfileExists(acct.Id, profileName);
        PersonaEngine.ValidateAccountBaseConfigClean(acct.Id);
        PersonaEngine.MaterializeProfileForDesktopLaunch(acct.Id, persona);

        var psi = _desktopWorkspaceLauncher.CreateBaseLaunchStartInfo(launchPlan);
        ApplyEnvironment(psi, acct, persona, accountPath, includeApiKeyFallback: acct.Type == "api_key");
        WriteActiveContext(accountPath, "desktop", null, acct, persona, ws, profileName, instructionsFile, psi.FileName, psi.ArgumentList.ToArray(), launchPlan.CommandPreview);
        WriteLaunchPlan("desktop", null, acct, persona, ws, accountPath, profileName, instructionsFile, psi.FileName, psi.ArgumentList.ToArray(), launchPlan.CommandPreview);
        return (launchPlan, psi, instructionsFile);
    }

    private void RegisterDesktopSession(Account acct, Persona persona, Workspace ws, DesktopWorkspaceLaunchPlan launchPlan, Process proc)
    {
        var isStoreLaunch = launchPlan.BaseLaunchMethod == "codex_app";
        var session = new Session
        {
            AccountId = acct.Id,
            PersonaId = persona.Id,
            WorkspaceId = ws.Id,
            Type = isStoreLaunch ? "desktop_store" : "desktop",
            AccountProvider = acct.ResolvedProvider,
            ProcessId = isStoreLaunch ? null : proc.Id,
            IsBestEffortUntracked = isStoreLaunch,
            RequestedProfileName = launchPlan.RequestedProfileName,
            RequestedCodexProfileName = launchPlan.RequestedCodexProfileName,
            ProfileLaunchMethod = launchPlan.ProfileLaunchMethod,
            ProfileVerificationStatus = launchPlan.ProfileVerificationStatus,
            ProfileLaunchCommandPreview = launchPlan.CommandPreview
        };

        if (isStoreLaunch)
        {
            var sessionDir = Path.Combine(JunctionManager.SwitcherDir, "sessions", session.Id);
            session.StartedMarkerPath = Path.Combine(sessionDir, "started.txt");
            session.ExitMarkerPath = Path.Combine(sessionDir, "exit.txt");
            session.StopMarkerPath = Path.Combine(sessionDir, "stop.txt");
            session.CodexPidPath = Path.Combine(sessionDir, "desktop-helper.pid");

            try
            {
                Directory.CreateDirectory(sessionDir);
                File.WriteAllText(session.StartedMarkerPath, DateTime.Now.ToString("O"));
                File.WriteAllText(session.CodexPidPath, proc.Id.ToString());
            }
            catch (Exception ex)
            {
                _log.Warn($"Desktop store session artifacts could not be created for helper PID {proc.Id}: {ex.Message}");
            }

            _log.Info("Desktop store session registered in best-effort/untracked mode; helper PID is not authoritative.");
        }
        else
        {
            _log.Info($"Desktop session registered with PID {proc.Id}.");
        }

        _sessionManager.Register(session);
    }

    private Session? CaptureRecoverableDesktopSession(string? previousActiveAccountId)
    {
        var candidates = _sessionManager.Active.Where(IsReliableDesktopRecoverySession).ToList();
        if (candidates.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(previousActiveAccountId))
        {
            var matching = candidates
                .Where(s => string.Equals(s.AccountId, previousActiveAccountId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefault();
            if (matching != null) return matching;
        }

        return candidates.OrderByDescending(s => s.StartTime).FirstOrDefault();
    }

    private bool TryRestoreDesktopLaunch(string? previousActiveAccountId, string currentAccountId)
    {
        var rollbackTarget = GetDesktopLaunchRollbackTarget(previousActiveAccountId, currentAccountId);
        if (string.IsNullOrWhiteSpace(rollbackTarget))
        {
            _log.Warn("Desktop launch rollback unavailable: no distinct previous managed account to restore.");
            return false;
        }

        try
        {
            _log.Warn($"Rolling back failed Desktop launch to previous account '{rollbackTarget}'.");
            JunctionManager.SwapToAccount(rollbackTarget, _log);
            _log.Info($"Desktop launch rollback restored account '{rollbackTarget}'.");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Desktop launch rollback failed for previous account '{rollbackTarget}'", ex);
            return false;
        }
    }

    private bool TryRelaunchPreviousDesktopSession(Session previousDesktopSession, out string detail)
    {
        detail = "No previous Desktop session was relaunched.";

        if (!IsReliableDesktopRecoverySession(previousDesktopSession))
        {
            detail = "The captured Desktop session is not trustworthy enough to relaunch.";
            return false;
        }

        var account = _accountManager.GetAccounts().FirstOrDefault(a => string.Equals(a.Id, previousDesktopSession.AccountId, StringComparison.OrdinalIgnoreCase));
        var persona = _config.LoadList<Persona>("personas").FirstOrDefault(p => string.Equals(p.Id, previousDesktopSession.PersonaId, StringComparison.OrdinalIgnoreCase));
        var workspace = _config.LoadList<Workspace>("workspaces").FirstOrDefault(w => string.Equals(w.Id, previousDesktopSession.WorkspaceId, StringComparison.OrdinalIgnoreCase));

        if (account == null || persona == null || workspace == null)
        {
            detail = $"Previous Desktop context is incomplete: account={(account?.Name ?? previousDesktopSession.AccountId)}, persona={(persona?.Name ?? previousDesktopSession.PersonaId)}, workspace={(workspace?.Name ?? previousDesktopSession.WorkspaceId)}.";
            return false;
        }

        try
        {
            var accountPath = JunctionManager.GetAccountProfilePath(account.Id);
            var profileName = PersonaEngine.GetProfileName(persona);
            var preparation = PrepareDesktopLaunch(account, persona, workspace, accountPath, profileName);
            var proc = _desktopWorkspaceLauncher.StartBaseLaunchAndQueueWorkspaceBinding(preparation.LaunchPlan, preparation.Psi);
            RegisterDesktopSession(account, persona, workspace, preparation.LaunchPlan, proc);
            detail = $"Previous Desktop relaunched as PID {proc.Id} for account '{account.Name}', workspace '{workspace.Name}'.";
            return true;
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return false;
        }
    }

    private InvalidOperationException RecoverDesktopLaunchAfterFailure(string? previousActiveAccountId, string currentAccountId, Session? previousDesktopSession, bool killSequenceCompleted, bool junctionSwapped, Exception failure)
    {
        var restoreSucceeded = TryRestoreDesktopLaunch(previousActiveAccountId, currentAccountId);
        var relaunchAttempted = false;
        var relaunchSucceeded = false;
        var manualRelaunchRequired = true;
        string relaunchDetail = "Previous Desktop relaunch was not attempted.";

        if (killSequenceCompleted && restoreSucceeded && IsReliableDesktopRecoverySession(previousDesktopSession))
        {
            relaunchAttempted = true;
            relaunchSucceeded = TryRelaunchPreviousDesktopSession(previousDesktopSession!, out relaunchDetail);
            manualRelaunchRequired = !relaunchSucceeded;
            if (relaunchSucceeded)
                _log.Info($"Desktop launch recovery relaunched the previous Desktop session: {relaunchDetail}");
            else
                _log.Error($"Desktop launch recovery restored the previous junction/account, but relaunch failed: {relaunchDetail}");
        }
        else if (restoreSucceeded && !killSequenceCompleted)
        {
            _log.Warn("Desktop launch recovery restored the previous junction/account, but the kill sequence did not complete cleanly enough to trust a relaunch. Manual relaunch is required.");
        }
        else if (restoreSucceeded)
        {
            _log.Warn("Desktop launch recovery restored the previous junction/account, but no trustworthy prior Desktop session was available to relaunch. Manual relaunch is required.");
        }
        else
        {
            _log.Warn("Desktop launch recovery could not restore the previous junction/account. Manual relaunch is required.");
        }

        if (!restoreSucceeded || (relaunchAttempted && !relaunchSucceeded))
            manualRelaunchRequired = true;

        var previousAccountName = previousDesktopSession == null
            ? null
            : _accountManager.GetAccounts().FirstOrDefault(a => string.Equals(a.Id, previousDesktopSession.AccountId, StringComparison.OrdinalIgnoreCase))?.Name;
        var previousWorkspaceName = previousDesktopSession == null
            ? null
            : _config.LoadList<Workspace>("workspaces").FirstOrDefault(w => string.Equals(w.Id, previousDesktopSession.WorkspaceId, StringComparison.OrdinalIgnoreCase))?.Name;

        var result = BuildDesktopLaunchRecoveryResult(restoreSucceeded, relaunchAttempted, relaunchSucceeded, manualRelaunchRequired, previousAccountName, previousWorkspaceName);
        string finalMessage;
        if (!junctionSwapped)
            finalMessage = "Desktop launch failed before account switch completed. Manual Desktop relaunch is required.";
        else
            finalMessage = result.Message;

        _log.Error($"Desktop launch failed (killSequenceCompleted={killSequenceCompleted}, junctionSwapped={junctionSwapped}); restoreSucceeded={restoreSucceeded}, relaunchAttempted={relaunchAttempted}, relaunchSucceeded={relaunchSucceeded}, manualRelaunchRequired={manualRelaunchRequired}. {finalMessage}", failure);
        return new InvalidOperationException(finalMessage, failure);
    }

    private static bool IsSafeEnvironmentVariableName(string name) =>
        !string.IsNullOrWhiteSpace(name) && name.All(c => char.IsLetterOrDigit(c) || c == '_');
}
