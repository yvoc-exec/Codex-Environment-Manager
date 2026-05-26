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

    public static Uri BuildCodexWorkspaceUri(string workspacePath)
    {
        var variants = BuildCodexWorkspaceUriVariants(workspacePath);
        return new Uri(variants[0].ActivationUri);
    }

    public static List<DesktopDeepLinkVariant> BuildCodexWorkspaceUriVariants(string workspacePath)
    {
        var absolutePath = Path.GetFullPath(workspacePath);
        var windowsBackslashEncoded = Uri.EscapeDataString(absolutePath);
        var forwardSlashPath = absolutePath.Replace('\\', '/');
        var forwardSlashEncoded = Uri.EscapeDataString(forwardSlashPath);
        var fileUriValue = new Uri(absolutePath).AbsoluteUri;
        var fileUriEncoded = Uri.EscapeDataString(fileUriValue);
        var rawActivationUri = $"codex://threads/new?path={absolutePath}";

        return new List<DesktopDeepLinkVariant>
        {
            CreateVariant(
                id: "windows_backslash",
                label: "Windows backslash absolute path",
                pathKind: "windows_backslash",
                absolutePath: absolutePath,
                encodedPathValue: windowsBackslashEncoded,
                activationUri: $"codex://threads/new?path={windowsBackslashEncoded}",
                includedInAutomaticAttempts: true),
            CreateVariant(
                id: "forward_slash",
                label: "Forward-slash absolute path",
                pathKind: "forward_slash",
                absolutePath: absolutePath,
                encodedPathValue: forwardSlashEncoded,
                activationUri: $"codex://threads/new?path={forwardSlashEncoded}",
                includedInAutomaticAttempts: true),
            CreateVariant(
                id: "file_uri",
                label: "file:/// URI",
                pathKind: "file_uri",
                absolutePath: absolutePath,
                encodedPathValue: fileUriEncoded,
                activationUri: $"codex://threads/new?path={fileUriEncoded}",
                includedInAutomaticAttempts: true),
            CreateVariant(
                id: "raw_absolute_path",
                label: "Raw absolute path",
                pathKind: "raw_absolute_path",
                absolutePath: absolutePath,
                encodedPathValue: absolutePath,
                activationUri: rawActivationUri,
                includedInAutomaticAttempts: false)
        };
    }

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

        plan.DeepLinkVariants = BuildCodexWorkspaceUriVariants(ws.Path);
        plan.DeepLinkUri = plan.DeepLinkVariants[0].ActivationUri;

        if (!string.IsNullOrWhiteSpace(detection.CliPath))
        {
            plan.BaseLaunchMethod = "codex_app";
            plan.CanLaunch = true;
            plan.ProfileLaunchMethod = "codex app -c profile";
        }
        else if (!string.IsNullOrWhiteSpace(detection.ExecutablePath))
        {
            plan.BaseLaunchMethod = "desktop_exe";
            plan.CanLaunch = true;
            plan.ProfileLaunchMethod = "desktop_exe";
        }
        else
        {
            plan.BaseLaunchMethod = "unavailable";
            plan.CanLaunch = false;
            plan.ProfileLaunchMethod = "unavailable";
        }

        if (!plan.CanLaunch)
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

            psi = _processManager.CreateCodexAppProcessStartInfo(plan.CodexHome, plan.WorkspacePath, plan.CodexProfileName);
            plan.ProfileLaunchMethod = "codex app -c profile";
            plan.ProfileOverrideArgs = BuildCodexAppProfileOverrideArgs(plan.WorkspacePath, plan.CodexProfileName);
            plan.ProfileVerificationStatus = "Profile override passed";
            plan.CommandPreview = BuildCodexAppCommandPreview(plan.CodexProfileName, plan.WorkspacePath);
        }
        else
        {
            throw new InvalidOperationException(plan.FailureReason ?? "No valid Desktop launch method is available.");
        }

        return psi;
    }

    private static string[] BuildCodexAppProfileOverrideArgs(string workspacePath, string codexProfileName) =>
        new[] { "app", "-c", $@"profile=""{codexProfileName}""", workspacePath };

    private static string BuildCodexAppCommandPreview(string codexProfileName, string workspacePath) =>
        $"codex app -c profile=\"{codexProfileName}\" {CodexProcessManager.QuoteForCmd(workspacePath)}";

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
            $"baseMethod={plan.BaseLaunchMethod}, protocolDetected={plan.ProtocolDetected}, uri={plan.DeepLinkUri}");

        var proc = Process.Start(psi) ?? throw new InvalidOperationException("Desktop launch returned no process handle.");
        plan.ResolvedDesktopProcessId = proc.Id;
        plan.LaunchStatus = "Desktop launched.";
        WriteLaunchPlanArtifact(plan);
        _log.Info($"Desktop launched PID {proc.Id} using {plan.BaseLaunchMethod}.");

        onStarted?.Invoke(proc);

        _ = Task.Run(() => AttemptWorkspaceBindingAsync(plan));
        return proc;
    }

    private async Task AttemptWorkspaceBindingAsync(DesktopWorkspaceLaunchPlan plan)
    {
        try
        {
            if (!plan.ProtocolDetected)
            {
                plan.BindingMethod = "bridge";
                plan.BindingSucceeded = false;
                plan.FallbackUsed = true;
                _log.Info(
                    $"Desktop workspace binding skipped deep-link because protocol was not detected. " +
                    $"Falling back to bridge for workspace={plan.WorkspaceName} ({plan.WorkspaceId}) path={plan.WorkspacePath}");
                DesktopWorkspaceBridge.ScheduleOpenWorkspace(plan.WorkspacePath, _log);
                WriteLaunchPlanArtifact(plan);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            var automaticVariants = plan.DeepLinkVariants.FindAll(v => v.IncludedInAutomaticAttempts);
            for (var attemptIndex = 0; attemptIndex < automaticVariants.Count; attemptIndex++)
            {
                var variant = automaticVariants[attemptIndex];
                var attemptNumber = attemptIndex + 1;
                try
                {
                    _log.Info(
                        $"Attempting ActivationSucceeded handoff attempt {attemptNumber}/3: variant={variant.Id} ({variant.Label}), uri={variant.ActivationUri}");
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = variant.ActivationUri,
                        UseShellExecute = true,
                        WorkingDirectory = plan.WorkspacePath
                    };

                    var helper = Process.Start(startInfo);
                    const bool activationSucceeded = true;
                    plan.HelperProcessId = helper?.Id;
                    plan.AttemptedDeepLinkVariantId = variant.Id;
                    plan.AttemptedDeepLinkVariantLabel = variant.Label;
                    plan.BindingMethod = "protocol";
                    plan.BindingSucceeded = null;
                    plan.FallbackUsed = false;
                    plan.DeepLinkAttemptResults.Add(new DesktopDeepLinkAttemptResult
                    {
                        VariantId = variant.Id,
                        VariantLabel = variant.Label,
                        AttemptedAt = DateTime.UtcNow.ToString("O"),
                        AttemptNumber = attemptNumber,
                        ActivationSucceeded = activationSucceeded,
                        HelperProcessId = helper?.Id,
                        ExceptionMessage = null
                    });
                    WriteLaunchPlanArtifact(plan);
                    _log.Info(
                        $"Protocol activation request succeeded: variant={variant.Id}, label={variant.Label}, activationSucceeded={activationSucceeded}, helperPid={(helper?.Id.ToString() ?? "<none>")}, desktopPid={(plan.ResolvedDesktopProcessId?.ToString() ?? "<unknown>")}; workspace binding confirmation remains unknown.");
                    _log.Info("ActivationSucceeded recorded; DesktopOpenedNewThread, ActualDesktopWorkingDirectory, and WorkspaceMatchStatus remain unknown. Pass 65 uses one activation request per safe variant with no per-variant retries.");
                    return;
                }
                catch (Exception ex)
                {
                    plan.DeepLinkAttemptResults.Add(new DesktopDeepLinkAttemptResult
                    {
                        VariantId = variant.Id,
                        VariantLabel = variant.Label,
                        AttemptedAt = DateTime.UtcNow.ToString("O"),
                        AttemptNumber = attemptNumber,
                        ActivationSucceeded = false,
                        HelperProcessId = null,
                        ExceptionMessage = ex.ToString()
                    });
                    _log.Warn(
                        $"ActivationSucceeded=false for deep-link variant={variant.Id} ({variant.Label}) attempt {attemptNumber}/3: {ex}");
                    WriteLaunchPlanArtifact(plan);
                    if (attemptNumber < automaticVariants.Count)
                        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                }
            }

            plan.BindingMethod = "protocol+bridge";
            plan.BindingSucceeded = false;
            plan.FallbackUsed = true;
            _log.Warn(
                $"Codex deep-link workspace binding failed after attempting all safe variants; falling back to DesktopWorkspaceBridge for " +
                $"workspace={plan.WorkspaceName} ({plan.WorkspaceId}) path={plan.WorkspacePath}");
            DesktopWorkspaceBridge.ScheduleOpenWorkspace(plan.WorkspacePath, _log);
            WriteLaunchPlanArtifact(plan);
        }
        catch (Exception ex)
        {
            plan.BindingMethod = "bridge";
            plan.BindingSucceeded = false;
            plan.FallbackUsed = true;
            plan.FailureReason = ex.Message;
            _log.Warn($"Workspace binding helper failed unexpectedly; falling back to bridge: {ex.Message}");
            DesktopWorkspaceBridge.ScheduleOpenWorkspace(plan.WorkspacePath, _log);
            WriteLaunchPlanArtifact(plan);
        }
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

        foreach (var variant in plan.DeepLinkVariants)
        {
            _log.Info(
                $"Desktop deep-link variant preflight: id={variant.Id}, label={variant.Label}, pathKind={variant.PathKind}, " +
                $"autoAttempt={variant.IncludedInAutomaticAttempts}, activationUri={variant.ActivationUri}, manualCommand={variant.ManualPowerShellCommand}");
        }
    }

    private void WriteLaunchPlanArtifact(DesktopWorkspaceLaunchPlan plan)
    {
        try
        {
            Directory.CreateDirectory(JunctionManager.SwitcherDir);
            var json = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });
            var jsonPath = Path.Combine(JunctionManager.SwitcherDir, "last_desktop_launch_plan.json");
            File.WriteAllText(jsonPath, json);

            var textPath = Path.Combine(JunctionManager.SwitcherDir, "last_desktop_deeplinks.txt");
            File.WriteAllText(textPath, BuildLaunchPlanDiagnosticsText(plan));

            _log.Info(
                $"Desktop deep-link diagnostics artifact written: json={jsonPath}, text={textPath}, variants={plan.DeepLinkVariants.Count}, attempts={plan.DeepLinkAttemptResults.Count}");
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to write desktop launch diagnostics artifacts: {ex.Message}");
        }
    }

    public static string BuildManualPowerShellStartProcessCommand(string activationUri)
    {
        var escaped = activationUri.Replace("'", "''", StringComparison.Ordinal);
        return $"Start-Process '{escaped}'";
    }

    private static DesktopDeepLinkVariant CreateVariant(
        string id,
        string label,
        string pathKind,
        string absolutePath,
        string encodedPathValue,
        string activationUri,
        bool includedInAutomaticAttempts)
    {
        return new DesktopDeepLinkVariant
        {
            Id = id,
            Label = label,
            PathKind = pathKind,
            AbsolutePath = absolutePath,
            EncodedPathValue = encodedPathValue,
            ActivationUri = activationUri,
            ManualPowerShellCommand = BuildManualPowerShellStartProcessCommand(activationUri),
            IncludedInAutomaticAttempts = includedInAutomaticAttempts
        };
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
            sb.AppendLine("Attempt Policy: one activation request per safe variant; no per-variant retries in Pass 65.");
            sb.AppendLine($"Automatic Attempt Order: {string.Join(", ", plan.DeepLinkVariants.FindAll(v => v.IncludedInAutomaticAttempts).ConvertAll(v => v.Id))}");
        sb.AppendLine($"DesktopOpenedNewThread: {plan.DesktopOpenedNewThread}");
        sb.AppendLine($"ActualDesktopWorkingDirectory: {plan.ActualDesktopWorkingDirectory}");
        sb.AppendLine($"WorkspaceMatchStatus: {plan.WorkspaceMatchStatus}");
        sb.AppendLine();
        sb.AppendLine("Variants:");

        foreach (var variant in plan.DeepLinkVariants)
        {
            sb.AppendLine($"- Id: {variant.Id}");
            sb.AppendLine($"  Label: {variant.Label}");
            sb.AppendLine($"  Source Path Form: {variant.PathKind}");
            sb.AppendLine($"  Absolute Path: {variant.AbsolutePath}");
            sb.AppendLine($"  Encoded Value: {variant.EncodedPathValue}");
            sb.AppendLine($"  Final URI: {variant.ActivationUri}");
            sb.AppendLine($"  Auto Attempted: {variant.IncludedInAutomaticAttempts}");
            sb.AppendLine($"  PowerShell Command: {variant.ManualPowerShellCommand}");
        }

        sb.AppendLine();
        sb.AppendLine("Attempt Results:");
        if (plan.DeepLinkAttemptResults.Count == 0)
        {
            sb.AppendLine("  <none>");
        }
        else
        {
            foreach (var attempt in plan.DeepLinkAttemptResults)
            {
                sb.AppendLine($"- Attempt Number: {attempt.AttemptNumber}");
                sb.AppendLine($"  Variant Id: {attempt.VariantId}");
                sb.AppendLine($"  Variant Label: {attempt.VariantLabel}");
                sb.AppendLine($"  Attempted At: {attempt.AttemptedAt}");
                sb.AppendLine($"  Activation Succeeded: {attempt.ActivationSucceeded}");
                sb.AppendLine($"  Helper Process Id: {attempt.HelperProcessId?.ToString() ?? "<none>"}");
                sb.AppendLine($"  Exception Message: {attempt.ExceptionMessage ?? "<none>"}");
            }
        }

        return sb.ToString();
    }
}
