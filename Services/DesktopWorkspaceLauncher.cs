using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodexEnvironmentManager.Models;

namespace CodexEnvironmentManager.Services;

public sealed class DesktopDeepLinkVariant
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string PathKind { get; set; } = "";
    public string AbsolutePath { get; set; } = "";
    public string EncodedPathValue { get; set; } = "";
    public string ActivationUri { get; set; } = "";
    public string ManualPowerShellCommand { get; set; } = "";
    public bool IncludedInAutomaticAttempts { get; set; }
}

public sealed class DesktopDeepLinkAttemptResult
{
    public string VariantId { get; set; } = "";
    public string VariantLabel { get; set; } = "";
    public string AttemptedAt { get; set; } = "";
    public int AttemptNumber { get; set; }
    public bool ActivationSucceeded { get; set; }
    public int? HelperProcessId { get; set; }
    public string? ExceptionMessage { get; set; }
}

public sealed class DesktopWorkspaceLaunchPlan
{
    public string LaunchId { get; set; } = Guid.NewGuid().ToString("N");
    public string GeneratedAt { get; set; } = DateTime.Now.ToString("O");

    public string AccountId { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string CodexHome { get; set; } = "";

    public string PersonaId { get; set; } = "";
    public string PersonaName { get; set; } = "";
    public string CodexProfileName { get; set; } = "";
    public string RequestedProfileName { get; set; } = "";
    public string RequestedCodexProfileName { get; set; } = "";
    public string ProfileLaunchMethod { get; set; } = "";
    public string ProfileVerificationStatus { get; set; } = "Profile unverified";
    public string CommandPreview { get; set; } = "";
    public string[] ProfileOverrideArgs { get; set; } = Array.Empty<string>();

    public string WorkspaceId { get; set; } = "";
    public string WorkspaceName { get; set; } = "";
    public string WorkspacePath { get; set; } = "";

    public string ProtocolScheme { get; set; } = "codex";
    public string? ProtocolSource { get; set; }
    public string? ProtocolCommand { get; set; }
    public bool ProtocolDetected { get; set; }

    public string? DesktopExecutablePath { get; set; }
    public string? CliFallbackPath { get; set; }
    public string? StorePackageFamilyName { get; set; }
    public string? StoreInstallLocation { get; set; }
    public string? PackagedExePath { get; set; }

    public string BaseLaunchMethod { get; set; } = "";
    public bool CanLaunch { get; set; }

    public string DeepLinkUri { get; set; } = "";
    public List<DesktopDeepLinkVariant> DeepLinkVariants { get; set; } = new();
    public List<DesktopDeepLinkAttemptResult> DeepLinkAttemptResults { get; set; } = new();
    public string? AttemptedDeepLinkVariantId { get; set; }
    public string? AttemptedDeepLinkVariantLabel { get; set; }
    public string DesktopOpenedNewThread { get; set; } = "unknown";
    public string ActualDesktopWorkingDirectory { get; set; } = "unknown";
    public string WorkspaceMatchStatus { get; set; } = "unknown";
    public string? BindingMethod { get; set; }
    public bool? BindingSucceeded { get; set; }
    public bool FallbackUsed { get; set; }
    public int? HelperProcessId { get; set; }
    public int? ResolvedDesktopProcessId { get; set; }
    public string? LaunchStatus { get; set; }
    public string? FailureReason { get; set; }
}

public sealed class DesktopWorkspaceLauncher
{
    private readonly CodexProcessManager _processManager;
    private readonly LogService _log;

    public DesktopWorkspaceLauncher(CodexProcessManager processManager, LogService log)
    {
        _processManager = processManager;
        _log = log;
    }

    // Deep-link URI generation helpers removed. CEM no longer uses codex:// protocol activation
    // for workspace binding because it is unreliable for Store-installed Codex.

    public DesktopWorkspaceLaunchPlan BuildLaunchPlan(Account acct, Persona persona, Workspace ws, string accountPath, string profileName)
    {
        ValidateLaunchInputs(acct, persona, ws, accountPath);

        var detection = _processManager.DetectCodexDesktop();
        var plan = new DesktopWorkspaceLaunchPlan
        {
            AccountId = acct.Id,
            AccountName = acct.Name,
            CodexHome = accountPath,
            PersonaId = persona.Id,
            PersonaName = persona.Name,
            CodexProfileName = profileName,
            RequestedProfileName = persona.Name,
            RequestedCodexProfileName = profileName,
            WorkspaceId = ws.Id,
            WorkspaceName = ws.Name,
            WorkspacePath = Path.GetFullPath(ws.Path),
            ProtocolDetected = detection.HasProtocol,
            ProtocolSource = detection.ProtocolSource,
            ProtocolCommand = detection.ProtocolCommand,
            DesktopExecutablePath = detection.ExecutablePath,
            CliFallbackPath = detection.CliPath,
            StorePackageFamilyName = detection.StorePackageFamilyName,
            StoreInstallLocation = detection.StoreInstallLocation,
            PackagedExePath = detection.ExecutablePath,
        };

        plan.DeepLinkVariants = new List<DesktopDeepLinkVariant>();
        plan.DeepLinkUri = "";

        if (!string.IsNullOrWhiteSpace(detection.CliPath))
        {
            plan.BaseLaunchMethod = "codex_app";
            plan.CanLaunch = true;
            plan.ProfileLaunchMethod = "codex app --profile";
        }
        else if (!string.IsNullOrWhiteSpace(detection.ExecutablePath))
        {
            plan.BaseLaunchMethod = "desktop_exe";
            plan.CanLaunch = true;
            plan.ProfileLaunchMethod = "desktop_exe";
        }
        else if (detection.HasStoreApp)
        {
            plan.BaseLaunchMethod = "unavailable";
            plan.CanLaunch = false;
            plan.ProfileLaunchMethod = "unavailable";
            plan.FailureReason = "Microsoft Store Codex was detected, but Codex CLI was not found. Install Codex CLI or ensure codex is on PATH so CEM can launch Desktop via `codex app`.";
        }
        else
        {
            plan.BaseLaunchMethod = "unavailable";
            plan.CanLaunch = false;
            plan.ProfileLaunchMethod = "unavailable";
        }

        if (!plan.CanLaunch && string.IsNullOrWhiteSpace(plan.FailureReason))
        {
            plan.FailureReason = "No valid Desktop launch method was detected.";
            plan.ProfileVerificationStatus = "Profile unverified";
        }

        LogPlan(plan);
        WriteLaunchPlanArtifact(plan);
        return plan;
    }

    public ProcessStartInfo CreateBaseLaunchStartInfo(DesktopWorkspaceLaunchPlan plan)
    {
        if (!plan.CanLaunch)
            throw new InvalidOperationException(plan.FailureReason ?? "No valid Desktop launch method is available.");

        ProcessStartInfo psi;
        if (string.Equals(plan.BaseLaunchMethod, "desktop_exe", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(plan.DesktopExecutablePath) || !File.Exists(plan.DesktopExecutablePath))
                throw new FileNotFoundException("Codex Desktop executable is not available.", plan.DesktopExecutablePath);

            psi = new ProcessStartInfo
            {
                FileName = plan.DesktopExecutablePath,
                UseShellExecute = false,
                WorkingDirectory = plan.WorkspacePath
            };
            plan.ProfileLaunchMethod = "desktop_exe";
            plan.ProfileOverrideArgs = Array.Empty<string>();
            plan.ProfileVerificationStatus = "Profile unverified";
            plan.CommandPreview = BuildExecutableCommandPreview(plan.DesktopExecutablePath, plan.WorkspacePath);
        }
        else if (string.Equals(plan.BaseLaunchMethod, "codex_app", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(plan.CliFallbackPath))
                throw new FileNotFoundException("Codex CLI fallback is not available.");

            // codex app does not support --profile. Profile is materialized into config.toml before launch.
            psi = _processManager.CreateCodexAppProcessStartInfo(plan.CodexHome, plan.WorkspacePath, plan.CodexProfileName);
            plan.ProfileLaunchMethod = "codex app + materialized config.toml";
            plan.ProfileOverrideArgs = Array.Empty<string>();
            plan.ProfileVerificationStatus = "Selected CEM profile materialized into base config.toml because codex app does not support --profile.";
            plan.CommandPreview = BuildCodexAppCommandPreview(plan.WorkspacePath);
        }
        else
        {
            throw new InvalidOperationException(plan.FailureReason ?? "No valid Desktop launch method is available.");
        }

        return psi;
    }

    private static string BuildCodexAppCommandPreview(string workspacePath) =>
        $"codex app {CodexProcessManager.QuoteForCmd(workspacePath)}";

    private static string BuildExecutableCommandPreview(string executablePath, string workspacePath) =>
        $"{CodexProcessManager.QuoteForCmd(executablePath)} {CodexProcessManager.QuoteForCmd(workspacePath)}";

    public Process StartBaseLaunchAndQueueWorkspaceBinding(DesktopWorkspaceLaunchPlan plan, ProcessStartInfo psi, Action<Process>? onStarted = null)
    {
        if (!plan.CanLaunch)
            throw new InvalidOperationException(plan.FailureReason ?? "No valid Desktop launch method is available.");

        _log.Info(
            $"Desktop launch start: account={plan.AccountName} ({plan.AccountId}), CODEX_HOME={plan.CodexHome}, " +
            $"profile={plan.PersonaName} ({plan.PersonaId}), codexProfile={plan.CodexProfileName}, " +
            $"workspace={plan.WorkspaceName} ({plan.WorkspaceId}) path={plan.WorkspacePath}, " +
            $"baseMethod={plan.BaseLaunchMethod}, protocolDetected={plan.ProtocolDetected}");

        var proc = Process.Start(psi) ?? throw new InvalidOperationException("Desktop launch returned no process handle.");
        plan.ResolvedDesktopProcessId = proc.Id;
        plan.LaunchStatus = "Desktop launched.";
        _log.Info($"Desktop launched PID {proc.Id} using {plan.BaseLaunchMethod}.");

        onStarted?.Invoke(proc);

        if (string.Equals(plan.BaseLaunchMethod, "codex_app", StringComparison.OrdinalIgnoreCase))
        {
            plan.BindingMethod = "codex_app_args";
            plan.BindingSucceeded = null;
            plan.FallbackUsed = false;
            plan.LaunchStatus = "Desktop launched via codex app with workspace argument.";
            _log.Info(
                $"Desktop workspace binding omitted for codex_app launch; workspace path is already passed as CLI argument. " +
                $"workspace={plan.WorkspaceName} ({plan.WorkspaceId}) path={plan.WorkspacePath}");
        }
        else if (string.Equals(plan.BaseLaunchMethod, "desktop_exe", StringComparison.OrdinalIgnoreCase))
        {
            plan.BindingMethod = "desktop_exe";
            plan.BindingSucceeded = null;
            plan.FallbackUsed = false;
            plan.LaunchStatus = "Desktop launched directly; no protocol/deep-link binding attempted.";
            _log.Info(
                $"Desktop workspace binding omitted for desktop_exe launch; working directory is set to workspace. " +
                $"workspace={plan.WorkspaceName} ({plan.WorkspaceId}) path={plan.WorkspacePath}");
        }

        WriteLaunchPlanArtifact(plan);
        return proc;
    }

    private void ValidateLaunchInputs(Account acct, Persona persona, Workspace ws, string accountPath)
    {
        if (string.IsNullOrWhiteSpace(acct.Id))
            throw new InvalidOperationException("Selected account is invalid.");
        if (string.IsNullOrWhiteSpace(persona.Id))
            throw new InvalidOperationException("Selected persona is invalid.");
        if (string.IsNullOrWhiteSpace(accountPath) || !Directory.Exists(accountPath))
            throw new DirectoryNotFoundException($"Account profile path does not exist: {accountPath}");
        if (string.IsNullOrWhiteSpace(ws.Path) || !Directory.Exists(ws.Path))
            throw new DirectoryNotFoundException($"Workspace path does not exist: {ws.Path}");
    }

    private void LogPlan(DesktopWorkspaceLaunchPlan plan)
    {
        _log.Info(
            $"Desktop preflight: account={plan.AccountName} ({plan.AccountId}), CODEX_HOME={plan.CodexHome}, " +
            $"profile={plan.PersonaName} ({plan.PersonaId}), codexProfile={plan.CodexProfileName}, " +
            $"workspace={plan.WorkspaceName} ({plan.WorkspaceId}) path={plan.WorkspacePath}, " +
            $"baseMethod={plan.BaseLaunchMethod}, protocolDetected={plan.ProtocolDetected}, " +
            $"protocolSource={plan.ProtocolSource ?? "<none>"}, storePFN={plan.StorePackageFamilyName ?? "<none>"}, " +
            $"storeLocation={plan.StoreInstallLocation ?? "<none>"}, desktopExe={plan.DesktopExecutablePath ?? "<none>"}, cliFallback={plan.CliFallbackPath ?? "<none>"}");
    }

    private void WriteLaunchPlanArtifact(DesktopWorkspaceLaunchPlan plan)
    {
        try
        {
            Directory.CreateDirectory(JunctionManager.SwitcherDir);
            var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });
            var jsonPath = Path.Combine(JunctionManager.SwitcherDir, "last_desktop_launch_plan.json");
            File.WriteAllText(jsonPath, json);

            var textPath = Path.Combine(JunctionManager.SwitcherDir, "last_desktop_launch_plan.txt");
            File.WriteAllText(textPath, BuildLaunchPlanDiagnosticsText(plan));

            _log.Info(
                $"Desktop launch diagnostics artifact written: json={jsonPath}, text={textPath}");
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to write desktop launch diagnostics artifacts: {ex.Message}");
        }
    }

    private static string BuildLaunchPlanDiagnosticsText(DesktopWorkspaceLaunchPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Launch ID: {plan.LaunchId}");
        sb.AppendLine($"Generated At: {plan.GeneratedAt}");
        sb.AppendLine($"Selected Persona: {plan.PersonaName} ({plan.PersonaId})");
        sb.AppendLine($"Selected Codex Profile: {plan.CodexProfileName}");
        sb.AppendLine($"Requested Profile Name: {plan.RequestedProfileName}");
        sb.AppendLine($"Requested Codex Profile Name: {plan.RequestedCodexProfileName}");
        sb.AppendLine($"Profile Launch Method: {plan.ProfileLaunchMethod}");
        sb.AppendLine($"Profile Verification Status: {plan.ProfileVerificationStatus}");
        sb.AppendLine($"Command Preview: {plan.CommandPreview}");
        var profileOverrideArgs = plan.ProfileOverrideArgs ?? Array.Empty<string>();
        sb.AppendLine($"Profile Override Args: {(profileOverrideArgs.Length == 0 ? "<none>" : string.Join(" ", profileOverrideArgs.Select(CodexProcessManager.QuoteForCmd)))}");
        sb.AppendLine($"Expected Workspace Path: {plan.WorkspacePath}");
            sb.AppendLine($"Protocol Detected: {plan.ProtocolDetected}");
            sb.AppendLine($"Protocol Source: {plan.ProtocolSource ?? "<none>"}");
            sb.AppendLine($"Protocol Command: {plan.ProtocolCommand ?? "<none>"}");
            sb.AppendLine("Deep-link / protocol workspace binding: not used (workspace passed via launch args or working directory).");
        sb.AppendLine($"DesktopOpenedNewThread: {plan.DesktopOpenedNewThread}");
        sb.AppendLine($"ActualDesktopWorkingDirectory: {plan.ActualDesktopWorkingDirectory}");
        sb.AppendLine($"WorkspaceMatchStatus: {plan.WorkspaceMatchStatus}");

        return sb.ToString();
    }
}
