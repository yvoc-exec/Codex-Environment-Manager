using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CodexEnvironmentManager;
using CodexEnvironmentManager.Models;
using CodexEnvironmentManager.Services;
using CodexEnvironmentManager.Views;

static class Program
{
    private static int _failures;

    static int Main()
    {
        Run(nameof(Persona_DefaultApprovalsReviewer_IsUser), Persona_DefaultApprovalsReviewer_IsUser);
        Run(nameof(Persona_JsonMissingApprovalsReviewer_UsesUserDefault), Persona_JsonMissingApprovalsReviewer_UsesUserDefault);
        Run(nameof(BuildManagedProfileValues_IncludesApprovalsReviewer), BuildManagedProfileValues_IncludesApprovalsReviewer);
        Run(nameof(BuildCodexDeepLinkUri_EncodesWorkspacePathAndOmitsProfile), BuildCodexDeepLinkUri_EncodesWorkspacePathAndOmitsProfile);
        Run(nameof(BuildCodexDeepLinkVariants_IncludesThreeSafeVariantsAndRawArtifactVariant), BuildCodexDeepLinkVariants_IncludesThreeSafeVariantsAndRawArtifactVariant);
        Run(nameof(BuildCodexDeepLinkVariants_OrdersAutomaticAttempts_BackslashThenForwardSlashThenFileUri), BuildCodexDeepLinkVariants_OrdersAutomaticAttempts_BackslashThenForwardSlashThenFileUri);
        Run(nameof(BuildCodexDeepLinkVariants_OmitsProfileParameter), BuildCodexDeepLinkVariants_OmitsProfileParameter);
        Run(nameof(BuildCodexDeepLinkVariants_EncodesWindowsAbsolutePath), BuildCodexDeepLinkVariants_EncodesWindowsAbsolutePath);
        Run(nameof(BuildCodexDeepLinkVariants_EncodesForwardSlashAbsolutePath), BuildCodexDeepLinkVariants_EncodesForwardSlashAbsolutePath);
        Run(nameof(BuildCodexDeepLinkVariants_EncodesFileUriPathValue), BuildCodexDeepLinkVariants_EncodesFileUriPathValue);
        Run(nameof(BuildManualPowerShellStartProcessCommand_WrapsUriForCopyPaste), BuildManualPowerShellStartProcessCommand_WrapsUriForCopyPaste);
        Run(nameof(DesktopLaunchPlan_DefaultsDesktopReadbackFieldsToUnknown), DesktopLaunchPlan_DefaultsDesktopReadbackFieldsToUnknown);
        Run(nameof(BatchEscaping_EscapesSpecialCharacters), BatchEscaping_EscapesSpecialCharacters);
        Run(nameof(BatchTitleEscaping_EscapesSpecialCharacters), BatchTitleEscaping_EscapesSpecialCharacters);
        Run(nameof(NormalizeDesktopOverridePath_ReturnsNullForBlankInput), NormalizeDesktopOverridePath_ReturnsNullForBlankInput);
        Run(nameof(FirstRunWizard_BuildCompletedSettings_PreservesBlankDesktopPathAndMarksOnboardingComplete), FirstRunWizard_BuildCompletedSettings_PreservesBlankDesktopPathAndMarksOnboardingComplete);
        Run(nameof(ResolveManagedActiveProfileName_FallsBackToGeneratedProfileAndWarns), ResolveManagedActiveProfileName_FallsBackToGeneratedProfileAndWarns);
        Run(nameof(ShouldRemoveSessionAfterKill_OnlyWhenConfirmed), ShouldRemoveSessionAfterKill_OnlyWhenConfirmed);
        Run(nameof(ShouldRemoveSessionAfterKill_AuthoritativeRemovalDoesNotBypassMissingConfirmation), ShouldRemoveSessionAfterKill_AuthoritativeRemovalDoesNotBypassMissingConfirmation);
        Run(nameof(DesktopProcessTargetResolver_PrefersStandaloneDesktopOverCliCompanions_UsingRealisticExeNames), DesktopProcessTargetResolver_PrefersStandaloneDesktopOverCliCompanions_UsingRealisticExeNames);
        Run(nameof(DesktopProcessTargetResolver_ReturnsNullWhenDesktopTargetIsAmbiguous), DesktopProcessTargetResolver_ReturnsNullWhenDesktopTargetIsAmbiguous);
        Run(nameof(BestEffortDesktopKill_InconclusiveResolutionKeepsSessionVisible), BestEffortDesktopKill_InconclusiveResolutionKeepsSessionVisible);
        Run(nameof(BestEffortDesktopKill_ConfirmedResolutionRemovesOnlyDesktopSession), BestEffortDesktopKill_ConfirmedResolutionRemovesOnlyDesktopSession);
        Run(nameof(BestEffortDesktopKill_StalePlaceholderWithoutArtifactsAndWithoutLiveDesktop_IsRetired), BestEffortDesktopKill_StalePlaceholderWithoutArtifactsAndWithoutLiveDesktop_IsRetired);
        Run(nameof(BestEffortDesktopKill_AmbiguousLiveDesktop_KeepsSessionVisible), BestEffortDesktopKill_AmbiguousLiveDesktop_KeepsSessionVisible);
        Run(nameof(RegisterDesktopSession_CreatesArtifactsForDesktopStoreSessions), RegisterDesktopSession_CreatesArtifactsForDesktopStoreSessions);
        Run(nameof(RecoverDesktopLaunchAfterFailure_UsesPreSwapWordingWhenJunctionWasNeverSwapped), RecoverDesktopLaunchAfterFailure_UsesPreSwapWordingWhenJunctionWasNeverSwapped);
        Run(nameof(SessionManager_Constructor_RequiresDesktopTerminator), SessionManager_Constructor_RequiresDesktopTerminator);
        Run(nameof(MainWindow_CompositionRoot_WiresProcessManagerIntoSessionManager), MainWindow_CompositionRoot_WiresProcessManagerIntoSessionManager);
        Run(nameof(MainWindow_Xaml_ReplacesMojibakeGlyphsWithSafeLabels), MainWindow_Xaml_ReplacesMojibakeGlyphsWithSafeLabels);
        Run(nameof(DesktopLaunchRollbackTarget_ReturnsPreviousManagedAccountWhenDifferent), DesktopLaunchRollbackTarget_ReturnsPreviousManagedAccountWhenDifferent);
        Run(nameof(DesktopLaunchRollbackTarget_ReturnsNullForMissingOrSameAccount), DesktopLaunchRollbackTarget_ReturnsNullForMissingOrSameAccount);
        Run(nameof(DesktopRecoverySession_IsReliableOnlyForRealDesktopSessions), DesktopRecoverySession_IsReliableOnlyForRealDesktopSessions);
        Run(nameof(BuildDesktopLaunchRecoveryResult_ReportsRestoreAndRelaunchWithoutClaimingSuccess), BuildDesktopLaunchRecoveryResult_ReportsRestoreAndRelaunchWithoutClaimingSuccess);
        Run(nameof(BuildDesktopLaunchRecoveryResult_RequiresManualRelaunchWhenRecoveryIsUntrusted), BuildDesktopLaunchRecoveryResult_RequiresManualRelaunchWhenRecoveryIsUntrusted);
        Run(nameof(ShouldPruneSession_SkipsBestEffortDesktopStoreSessions), ShouldPruneSession_SkipsBestEffortDesktopStoreSessions);
        Run(nameof(IsBestEffortDesktopSession_MatchesOnlyDesktopStorePlaceholderCases), IsBestEffortDesktopSession_MatchesOnlyDesktopStorePlaceholderCases);
        Run(nameof(SessionViewModel_TypeIcon_TreatsDesktopStoreAsDesktop), SessionViewModel_TypeIcon_TreatsDesktopStoreAsDesktop);

        if (_failures > 0)
        {
            Console.Error.WriteLine($"FAILED: {_failures} test(s)");
            return 1;
        }

        Console.WriteLine("All tests passed.");
        return 0;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"PASS: {name}");
        }
        catch (Exception ex)
        {
            _failures++;
            Console.Error.WriteLine($"FAIL: {name}: {ex.Message}");
        }
    }

    private static void Persona_DefaultApprovalsReviewer_IsUser()
    {
        var persona = new Persona();
        AssertEqual("user", persona.ApprovalsReviewer, "new personas should default approvals_reviewer to user");
    }

    private static void Persona_JsonMissingApprovalsReviewer_UsesUserDefault()
    {
        var persona = JsonSerializer.Deserialize<Persona>("{}") ?? throw new Exception("persona did not deserialize");
        AssertEqual("user", persona.ApprovalsReviewer, "deserializing old personas should keep the user default");
    }

    private static void BuildCodexDeepLinkUri_EncodesWorkspacePathAndOmitsProfile()
    {
        var uri = DesktopWorkspaceLauncher.BuildCodexWorkspaceUri(@"D:\Projects\AI Work\repo");

        AssertEqual("codex", uri.Scheme, "deep-link scheme");
        AssertEqual("threads", uri.Host, "deep-link host");
        AssertEqual("/new", uri.AbsolutePath, "deep-link path");
        AssertContains("path=D%3A%5CProjects%5CAI%20Work%5Crepo", uri.Query, "encoded workspace path should be present");
        AssertNotContains("profile=", uri.Query, "deep-link should not include undocumented profile parameter");
    }

    private static void BuildCodexDeepLinkVariants_IncludesThreeSafeVariantsAndRawArtifactVariant()
    {
        var variants = DesktopWorkspaceLauncher.BuildCodexWorkspaceUriVariants(@"D:\Projects\AI Work\repo");

        AssertEqual(4, variants.Count, "workspace deep-link variants count");
        AssertEqual("windows_backslash", variants[0].PathKind, "first variant should be Windows backslash");
        AssertEqual("forward_slash", variants[1].PathKind, "second variant should be forward slash");
        AssertEqual("file_uri", variants[2].PathKind, "third variant should be file URI");
        AssertEqual("raw_absolute_path", variants[3].PathKind, "fourth variant should be raw absolute path");
        AssertEqual(true, variants[0].IncludedInAutomaticAttempts, "first variant should be automatically attempted");
        AssertEqual(true, variants[1].IncludedInAutomaticAttempts, "second variant should be automatically attempted");
        AssertEqual(true, variants[2].IncludedInAutomaticAttempts, "third variant should be automatically attempted");
        AssertEqual(false, variants[3].IncludedInAutomaticAttempts, "raw artifact variant should not be automatically attempted");
    }

    private static void BuildCodexDeepLinkVariants_OrdersAutomaticAttempts_BackslashThenForwardSlashThenFileUri()
    {
        var variants = DesktopWorkspaceLauncher.BuildCodexWorkspaceUriVariants(@"D:\Projects\AI Work\repo");
        var automatic = variants.Where(v => v.IncludedInAutomaticAttempts).Select(v => v.PathKind).ToArray();

        AssertEqual(3, automatic.Length, "automatic attempt count");
        AssertEqual("windows_backslash", automatic[0], "first automatic attempt");
        AssertEqual("forward_slash", automatic[1], "second automatic attempt");
        AssertEqual("file_uri", automatic[2], "third automatic attempt");
    }

    private static void BuildCodexDeepLinkVariants_OmitsProfileParameter()
    {
        var variants = DesktopWorkspaceLauncher.BuildCodexWorkspaceUriVariants(@"D:\Projects\AI Work\repo");

        foreach (var variant in variants)
        {
            AssertNotContains("profile=", variant.ActivationUri, $"variant {variant.Id} should not include undocumented profile parameter");
        }
    }

    private static void BuildCodexDeepLinkVariants_EncodesWindowsAbsolutePath()
    {
        var variants = DesktopWorkspaceLauncher.BuildCodexWorkspaceUriVariants(@"D:\Projects\AI Work\repo");
        var variant = variants[0];

        AssertEqual("D%3A%5CProjects%5CAI%20Work%5Crepo", variant.EncodedPathValue, "windows absolute path encoding");
        AssertContains("path=D%3A%5CProjects%5CAI%20Work%5Crepo", variant.ActivationUri, "windows absolute path URI");
    }

    private static void BuildCodexDeepLinkVariants_EncodesForwardSlashAbsolutePath()
    {
        var variants = DesktopWorkspaceLauncher.BuildCodexWorkspaceUriVariants(@"D:\Projects\AI Work\repo");
        var variant = variants[1];

        AssertEqual("D%3A%2FProjects%2FAI%20Work%2Frepo", variant.EncodedPathValue, "forward slash absolute path encoding");
        AssertContains("path=D%3A%2FProjects%2FAI%20Work%2Frepo", variant.ActivationUri, "forward slash absolute path URI");
    }

    private static void BuildCodexDeepLinkVariants_EncodesFileUriPathValue()
    {
        var variants = DesktopWorkspaceLauncher.BuildCodexWorkspaceUriVariants(@"D:\Projects\AI Work\repo");
        var variant = variants[2];

        AssertEqual("file%3A%2F%2F%2FD%3A%2FProjects%2FAI%2520Work%2Frepo", variant.EncodedPathValue, "file URI path encoding");
        AssertContains("path=file%3A%2F%2F%2FD%3A%2FProjects%2FAI%2520Work%2Frepo", variant.ActivationUri, "file URI activation URI");
    }

    private static void BuildManualPowerShellStartProcessCommand_WrapsUriForCopyPaste()
    {
        var command = DesktopWorkspaceLauncher.BuildManualPowerShellStartProcessCommand("codex://threads/new?path=D:\\Projects\\O'Brien");

        AssertEqual("Start-Process 'codex://threads/new?path=D:\\Projects\\O''Brien'", command, "manual PowerShell command should wrap and escape single quotes");
    }

    private static void DesktopLaunchPlan_DefaultsDesktopReadbackFieldsToUnknown()
    {
        var plan = new DesktopWorkspaceLaunchPlan();

        AssertEqual("unknown", plan.DesktopOpenedNewThread, "desktop opened new thread placeholder");
        AssertEqual("unknown", plan.ActualDesktopWorkingDirectory, "desktop working directory placeholder");
        AssertEqual("unknown", plan.WorkspaceMatchStatus, "workspace match status placeholder");
        AssertEqual<bool?>(null, plan.BindingSucceeded, "binding confirmation should default to unknown");
    }

    private static void BatchEscaping_EscapesSpecialCharacters()
    {
        var input = "A B\"C&D|E<F>G%H^I!";
        var escaped = CodexProcessManager.EscapeBatchValue(input);

        AssertEqual("A B^\"C^&D^|E^<F^>G%%H^^I^!", escaped, "batch value escaping");
        AssertEqual("\"A B^\"C^&D^|E^<F^>G%%H^^I^!\"", CodexProcessManager.QuoteForCmd(input), "cmd argument quoting");
    }

    private static void BatchTitleEscaping_EscapesSpecialCharacters()
    {
        var input = "A B\"C&D|E<F>G%H^I!";
        var escaped = LauncherService.EscapeBatchTitle(input);

        AssertEqual("A B^\"C^&D^|E^<F^>G%%H^^I^!", escaped, "batch title escaping");
    }

    private static void NormalizeDesktopOverridePath_ReturnsNullForBlankInput()
    {
        AssertEqual<string?>(null, CodexProcessManager.NormalizeDesktopOverridePath(""), "blank desktop override should clear the in-memory override");
        AssertEqual<string?>(null, CodexProcessManager.NormalizeDesktopOverridePath("   "), "whitespace desktop override should clear the in-memory override");
        AssertEqual(@"D:\Codex\Desktop.exe", CodexProcessManager.NormalizeDesktopOverridePath(@"D:\Codex\Desktop.exe"), "non-empty desktop override should be preserved");
    }

    private static void FirstRunWizard_BuildCompletedSettings_PreservesBlankDesktopPathAndMarksOnboardingComplete()
    {
        var existing = new AppSettings { CodexDesktopPath = @"D:\old\Desktop.exe", OnboardingCompleted = false };
        var completed = FirstRunWizard.BuildCompletedSettings(existing, "");

        AssertEqual("", completed.CodexDesktopPath, "first-run completion should persist a blank desktop path");
        AssertEqual(true, completed.OnboardingCompleted, "first-run completion should mark onboarding complete");
    }

    private static void ResolveManagedActiveProfileName_FallsBackToGeneratedProfileAndWarns()
    {
        var persona = new Persona { Name = "Implementor GPT 5.4 Mini High" };
        var warnings = new List<string>();
        var active = PersonaEngine.ResolveManagedActiveProfileName("cem_missing_profile", new[] { persona }, warnings.Add);

        AssertEqual(PersonaEngine.GetProfileName(persona), active, "missing root profile should fall back to generated profile");
        AssertEqual(1, warnings.Count, "missing root profile should warn once");
        AssertContains("missing", warnings[0], "warning should explain the missing stored profile");
    }

    private static void ShouldRemoveSessionAfterKill_OnlyWhenConfirmed()
    {
        AssertEqual(false, SessionManager.ShouldRemoveSessionAfterKill(false), "failed kill should not remove the session");
        AssertEqual(true, SessionManager.ShouldRemoveSessionAfterKill(true), "confirmed kill should remove the session");
    }

    private static void ShouldRemoveSessionAfterKill_AuthoritativeRemovalDoesNotBypassMissingConfirmation()
    {
        AssertEqual(false, SessionManager.ShouldRemoveSessionAfterKill(false, authoritativeRemovalRequested: false), "unconfirmed kill without authority should not remove the session");
        AssertEqual(true, SessionManager.ShouldRemoveSessionAfterKill(true, authoritativeRemovalRequested: false), "confirmed kill without authority should remove the session");
        AssertEqual(false, SessionManager.ShouldRemoveSessionAfterKill(false, authoritativeRemovalRequested: true), "authoritative removal must not retire the session when kill confirmation is unavailable");
    }

    private static void DesktopProcessTargetResolver_PrefersStandaloneDesktopOverCliCompanions_UsingRealisticExeNames()
    {
        var target = DesktopProcessTargetResolver.Resolve(new[]
        {
            new CodexProcessSnapshot(101, "Codex.exe", @"C:\Program Files\OpenAI\Codex\Codex.exe"),
            new CodexProcessSnapshot(202, "Codex.exe", @"C:\Users\user\AppData\Roaming\npm\codex.cmd --cd D:\work --profile implementor"),
            new CodexProcessSnapshot(303, "Codex.exe", @"C:\Users\user\AppData\Roaming\npm\codex.cmd --cd D:\work\other --profile reviewer")
        });

        AssertEqual(101, target?.ProcessId ?? -1, "desktop target resolver should choose the standalone Desktop process instead of CLI companions");
    }

    private static void DesktopProcessTargetResolver_ReturnsNullWhenDesktopTargetIsAmbiguous()
    {
        var target = DesktopProcessTargetResolver.Resolve(new[]
        {
            new CodexProcessSnapshot(101, "Codex.exe", @"C:\Program Files\OpenAI\Codex\Codex.exe"),
            new CodexProcessSnapshot(102, "Codex.exe", @"C:\Program Files\OpenAI\Codex\Codex.exe")
        });

        AssertEqual<DesktopProcessTarget?>(null, target, "ambiguous desktop candidates should not trigger a process kill");
    }

    private static void BestEffortDesktopKill_InconclusiveResolutionKeepsSessionVisible()
    {
        var sessionManager = CreateSessionManager(new FakeBestEffortDesktopTerminator
        {
            Result = DesktopKillAttemptResult.Inconclusive("desktop target could not be resolved")
        });

        var session = new Session
        {
            AccountId = "acct-a",
            PersonaId = "persona-a",
            WorkspaceId = "ws-a",
            Type = "desktop_store",
            IsBestEffortUntracked = true,
            StartedMarkerPath = @"C:\temp\started.txt"
        };
        sessionManager.Register(session);

        var result = sessionManager.KillSession(session.Id);

        AssertEqual(false, result.KillConfirmed, "inconclusive desktop kill should not be reported as confirmed");
        AssertEqual(false, result.SessionRemoved, "inconclusive desktop kill should keep the row visible");
        AssertEqual(1, sessionManager.Active.Count, "inconclusive desktop kill should not remove the tracked session");
    }

    private static void BestEffortDesktopKill_ConfirmedResolutionRemovesOnlyDesktopSession()
    {
        var sessionManager = CreateSessionManager(new FakeBestEffortDesktopTerminator
        {
            Result = DesktopKillAttemptResult.Confirmed(4242, "terminated the standalone desktop instance")
        });

        var desktopSession = new Session
        {
            AccountId = "acct-a",
            PersonaId = "persona-a",
            WorkspaceId = "ws-a",
            Type = "desktop_store",
            IsBestEffortUntracked = true
        };
        var cliSession = new Session
        {
            AccountId = "acct-b",
            PersonaId = "persona-b",
            WorkspaceId = "ws-b",
            Type = "cli"
        };
        sessionManager.Register(desktopSession);
        sessionManager.Register(cliSession);

        var result = sessionManager.KillSession(desktopSession.Id);

        AssertEqual(true, result.KillConfirmed, "targeted desktop kill should be confirmed");
        AssertEqual(true, result.SessionRemoved, "targeted desktop kill should remove the desktop session");
        AssertEqual(1, sessionManager.Active.Count, "only one session should remain after killing the desktop session");
        AssertEqual(cliSession.Id, sessionManager.Active.Single().Id, "the unrelated CLI companion should remain tracked");
    }

    private static void BestEffortDesktopKill_StalePlaceholderWithoutArtifactsAndWithoutLiveDesktop_IsRetired()
    {
        var sessionManager = CreateSessionManager(new FakeBestEffortDesktopTerminator
        {
            InspectResult = BestEffortDesktopSessionInspection.NoDesktop("no live desktop detected"),
            KillResult = DesktopKillAttemptResult.Inconclusive("should not be used")
        });

        var session = new Session
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "desktop_store",
            IsBestEffortUntracked = true,
            StartedMarkerPath = "",
            ExitMarkerPath = "",
            StopMarkerPath = ""
        };

        sessionManager.Register(session);
        var result = sessionManager.KillSession(session.Id);

        AssertEqual(true, result.KillConfirmed, "stale placeholder retirement should be treated as confirmed session retirement");
        AssertEqual(true, result.SessionRemoved, "stale placeholder should be removed");
        AssertContains("stale", result.Message.ToLowerInvariant(), "message should explain stale placeholder retirement");
    }

    private static void BestEffortDesktopKill_AmbiguousLiveDesktop_KeepsSessionVisible()
    {
        var sessionManager = CreateSessionManager(new FakeBestEffortDesktopTerminator
        {
            InspectResult = BestEffortDesktopSessionInspection.Ambiguous("multiple desktop candidates"),
            KillResult = DesktopKillAttemptResult.Inconclusive("multiple desktop candidates")
        });

        var session = new Session
        {
            Type = "desktop_store",
            IsBestEffortUntracked = true,
            StartedMarkerPath = @"C:\temp\started.txt"
        };

        sessionManager.Register(session);
        var result = sessionManager.KillSession(session.Id);

        AssertEqual(false, result.SessionRemoved, "ambiguous live desktop state must not retire the row");
        AssertEqual(false, result.KillConfirmed, "ambiguous state must not be treated as confirmed");
    }

    private static void RegisterDesktopSession_CreatesArtifactsForDesktopStoreSessions()
    {
        var source = ReadRepoFile("Services/LauncherService.cs");

        AssertContains("var sessionDir = Path.Combine(JunctionManager.SwitcherDir, \"sessions\", session.Id);", source, "desktop_store registrations should create a dedicated session directory");
        AssertContains("session.StartedMarkerPath = Path.Combine(sessionDir, \"started.txt\");", source, "desktop_store registrations should assign a started marker");
        AssertContains("session.ExitMarkerPath = Path.Combine(sessionDir, \"exit.txt\");", source, "desktop_store registrations should assign an exit marker");
        AssertContains("session.StopMarkerPath = Path.Combine(sessionDir, \"stop.txt\");", source, "desktop_store registrations should assign a stop marker");
        AssertContains("session.CodexPidPath = Path.Combine(sessionDir, \"desktop-helper.pid\");", source, "desktop_store registrations should write the helper pid path");
        AssertContains("File.WriteAllText(session.StartedMarkerPath, DateTime.Now.ToString(\"O\"));", source, "desktop_store registrations should write a started marker");
        AssertContains("File.WriteAllText(session.CodexPidPath, proc.Id.ToString());", source, "desktop_store registrations should persist the helper PID");
    }

    private static void RecoverDesktopLaunchAfterFailure_UsesPreSwapWordingWhenJunctionWasNeverSwapped()
    {
        var source = ReadRepoFile("Services/LauncherService.cs");

        AssertContains("if (!junctionSwapped)", source, "recovery logic should distinguish failures before the junction swap");
        AssertContains("Desktop launch failed before account switch completed.", source, "pre-swap launch failures should use a clearer message");
    }

    private static void SessionManager_Constructor_RequiresDesktopTerminator()
    {
        var ctor = typeof(SessionManager).GetConstructors().Single();
        var parameters = ctor.GetParameters();

        AssertEqual(2, parameters.Length, "SessionManager should expose a two-parameter constructor");
        AssertEqual(typeof(ConfigService), parameters[0].ParameterType, "first SessionManager parameter should remain the config service");
        AssertEqual(typeof(IBestEffortDesktopTerminator), parameters[1].ParameterType, "second SessionManager parameter should be the desktop terminator dependency");
        AssertEqual(false, parameters[1].HasDefaultValue, "desktop terminator should be required instead of optional");
    }

    private static void MainWindow_CompositionRoot_WiresProcessManagerIntoSessionManager()
    {
        var source = ReadRepoFile("MainWindow.xaml.cs");
        var processLine = "_processManager = new CodexProcessManager(_log);";
        var sessionLine = "_sessionManager = new SessionManager(_config, _processManager);";

        AssertContains(processLine, source, "MainWindow should create the process manager");
        AssertContains(sessionLine, source, "MainWindow should pass the process manager into SessionManager");
        AssertEqual(true, source.IndexOf(processLine, StringComparison.Ordinal) < source.IndexOf(sessionLine, StringComparison.Ordinal), "MainWindow should initialize the process manager before SessionManager");
        AssertNotContains("new SessionManager(_config);", source, "MainWindow should no longer use the nullable SessionManager overload");
    }

    private static void MainWindow_Xaml_ReplacesMojibakeGlyphsWithSafeLabels()
    {
        var xaml = ReadRepoFile("MainWindow.xaml");

        AssertNotContains("â€”", xaml, "title-bar minimize glyph should no longer be mojibake");
        AssertNotContains("â–¡", xaml, "title-bar maximize glyph should no longer be mojibake");
        AssertNotContains("âœ•", xaml, "title-bar close glyph should no longer be mojibake");
        AssertNotContains("â—", xaml, "active indicator glyph should no longer be mojibake");
        AssertNotContains("âœŽ", xaml, "edit glyph should no longer be mojibake");
        AssertNotContains("ðŸ“‚", xaml, "open-folder glyph should no longer be mojibake");
        AssertNotContains("ðŸ–¥ï¸", xaml, "launch-desktop glyph should no longer be mojibake");
        AssertNotContains("âŒ¨ï¸", xaml, "launch-cli glyph should no longer be mojibake");
        AssertNotContains("ðŸ‘", xaml, "view-files glyph should no longer be mojibake");
        AssertNotContains("ðŸ”„", xaml, "resume-last glyph should no longer be mojibake");
        AssertNotContains("âš™", xaml, "settings glyph should no longer be mojibake");
        AssertNotContains("ðŸšª", xaml, "exit glyph should no longer be mojibake");

        AssertContains("Content=\"—\"", xaml, "title-bar minimize button should use a safe symbol");
        AssertContains("Content=\"□\"", xaml, "title-bar maximize button should use a safe symbol");
        AssertContains("Content=\"✕\"", xaml, "title-bar close button should use a safe symbol");
        AssertContains("Text=\"  ●\"", xaml, "account active indicator should use a safe dot");
        AssertContains("Content=\"Edit\"", xaml, "edit buttons should use plain text");
        AssertContains("Content=\"Delete\"", xaml, "delete buttons should use plain text");
        AssertContains("Content=\"Open Folder\"", xaml, "workspace open button should use plain text");
        AssertContains("Content=\"Settings\"", xaml, "settings button should use plain text");
        AssertContains("Content=\"Exit\"", xaml, "exit button should use plain text");
        AssertContains("Content=\"Launch Desktop\"", xaml, "desktop launch button should use plain text");
        AssertContains("Content=\"Launch CLI Companion\"", xaml, "CLI launch button should use plain text");
        AssertContains("Content=\"View Files\"", xaml, "view-files button should use plain text");
        AssertContains("Content=\"Resume Last\"", xaml, "resume-last button should use plain text");
    }

    private static void DesktopLaunchRollbackTarget_ReturnsPreviousManagedAccountWhenDifferent()
    {
        AssertEqual("acct-b", LauncherService.GetDesktopLaunchRollbackTarget("acct-b", "acct-a"), "different prior account should be restored");
    }

    private static void DesktopLaunchRollbackTarget_ReturnsNullForMissingOrSameAccount()
    {
        AssertEqual<string?>(null, LauncherService.GetDesktopLaunchRollbackTarget(null, "acct-a"), "missing prior account should not suggest rollback");
        AssertEqual<string?>(null, LauncherService.GetDesktopLaunchRollbackTarget("acct-a", "acct-a"), "same account should not suggest rollback");
    }

    private static void DesktopRecoverySession_IsReliableOnlyForRealDesktopSessions()
    {
        var reliable = new Session
        {
            Type = "desktop",
            AccountId = "acct-a",
            PersonaId = "persona-a",
            WorkspaceId = "ws-a",
            ProcessId = 4242,
            IsBestEffortUntracked = false
        };

        var storeSession = new Session
        {
            Type = "desktop_store",
            AccountId = "acct-a",
            PersonaId = "persona-a",
            WorkspaceId = "ws-a",
            IsBestEffortUntracked = true
        };

        var missingPid = new Session
        {
            Type = "desktop",
            AccountId = "acct-a",
            PersonaId = "persona-a",
            WorkspaceId = "ws-a"
        };

        AssertEqual(true, LauncherService.IsReliableDesktopRecoverySession(reliable), "real desktop sessions with a process id should be eligible for recovery relaunch");
        AssertEqual(false, LauncherService.IsReliableDesktopRecoverySession(storeSession), "desktop_store sessions should not be treated as reliable relaunch context");
        AssertEqual(false, LauncherService.IsReliableDesktopRecoverySession(missingPid), "desktop sessions without a process id should not be treated as reliable relaunch context");
    }

    private static void BuildDesktopLaunchRecoveryResult_ReportsRestoreAndRelaunchWithoutClaimingSuccess()
    {
        var result = LauncherService.BuildDesktopLaunchRecoveryResult(
            junctionRestored: true,
            relaunchAttempted: true,
            relaunchSucceeded: true,
            manualRelaunchRequired: false,
            previousAccountName: "Previous Account",
            previousWorkspaceName: "Previous Workspace");

        AssertEqual(true, result.JunctionRestored, "junction should be reported as restored");
        AssertEqual(true, result.RelaunchAttempted, "relaunch should be reported as attempted");
        AssertEqual(true, result.RelaunchSucceeded, "relaunch should be reported as successful");
        AssertEqual(false, result.ManualRelaunchRequired, "manual relaunch should not be required after successful recovery relaunch");
        AssertContains("restored", result.Message, "recovery message should mention the restore");
        AssertContains("relaunched", result.Message, "recovery message should mention the relaunch");
        AssertNotContains("Desktop launched.", result.Message, "recovery message must not overclaim a fresh launch");
    }

    private static void BuildDesktopLaunchRecoveryResult_RequiresManualRelaunchWhenRecoveryIsUntrusted()
    {
        var result = LauncherService.BuildDesktopLaunchRecoveryResult(
            junctionRestored: true,
            relaunchAttempted: false,
            relaunchSucceeded: false,
            manualRelaunchRequired: true,
            previousAccountName: "Previous Account",
            previousWorkspaceName: "Previous Workspace");

        AssertEqual(true, result.JunctionRestored, "junction should still be restored");
        AssertEqual(false, result.RelaunchAttempted, "relaunch should not be attempted without trustworthy context");
        AssertEqual(false, result.RelaunchSucceeded, "relaunch should not be reported as successful when it was not attempted");
        AssertEqual(true, result.ManualRelaunchRequired, "manual relaunch should be required when no trustworthy relaunch context exists");
        AssertContains("manual desktop relaunch", result.Message.ToLowerInvariant(), "message should tell the user that manual relaunch is required");
        AssertNotContains("Desktop launched.", result.Message, "manual recovery message must not overclaim launch success");
    }

    private static void ShouldPruneSession_SkipsBestEffortDesktopStoreSessions()
    {
        var session = new Session
        {
            Type = "desktop_store",
            IsBestEffortUntracked = true,
            ProcessId = 1234
        };

        AssertEqual(false, SessionManager.ShouldPruneSession(session), "best-effort desktop_store sessions should not be pruned solely from helper PID exit");
    }

    private static void IsBestEffortDesktopSession_MatchesOnlyDesktopStorePlaceholderCases()
    {
        var storeTracked = new Session
        {
            Type = "desktop_store",
            IsBestEffortUntracked = true
        };

        var storePlaceholder = new Session
        {
            Type = "desktop_store",
            IsBestEffortUntracked = false
        };

        var desktopTracked = new Session
        {
            Type = "desktop",
            IsBestEffortUntracked = false
        };

        var cliSession = new Session
        {
            Type = "cli",
            IsBestEffortUntracked = false
        };

        AssertEqual(true, SessionManager.IsBestEffortDesktopSession(storeTracked), "best-effort desktop_store session should be recognized");
        AssertEqual(true, SessionManager.IsBestEffortDesktopSession(storePlaceholder), "desktop_store type alone should be recognized");
        AssertEqual(false, SessionManager.IsBestEffortDesktopSession(desktopTracked), "tracked desktop session should not be treated as a best-effort placeholder");
        AssertEqual(false, SessionManager.IsBestEffortDesktopSession(cliSession), "cli session should not be treated as a best-effort desktop placeholder");
    }

    private static void SessionViewModel_TypeIcon_TreatsDesktopStoreAsDesktop()
    {
        var desktopVm = new SessionViewModel(new Session { Type = "desktop_store" });
        var cliVm = new SessionViewModel(new Session { Type = "cli" });

        AssertEqual("Desktop", desktopVm.TypeIcon, "desktop_store sessions should use the Desktop label");
        AssertEqual("CLI", cliVm.TypeIcon, "cli sessions should use the CLI label");
    }

    private static void BuildManagedProfileValues_IncludesApprovalsReviewer()
    {
        var persona = new Persona
        {
            ApprovalsReviewer = "auto_review",
            ConfigOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["model"] = "gpt-5.4"
            }
        };

        var values = PersonaEngine.BuildManagedProfileValues(persona, @"D:\cem\profile.instructions.md");
        AssertEqual("auto_review", values["approvals_reviewer"], "config profile block should include approvals_reviewer");
        AssertEqual(@"D:\cem\profile.instructions.md", values["model_instructions_file"], "model_instructions_file should be preserved");
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"{message}. Expected '{expected}', got '{actual}'.");
    }

    private static void AssertContains(string expected, string actual, string message)
    {
        if (actual is null || !actual.Contains(expected, StringComparison.Ordinal))
            throw new Exception($"{message}. Expected to find '{expected}' in '{actual}'.");
    }

    private static void AssertNotContains(string unexpected, string actual, string message)
    {
        if (actual is not null && actual.Contains(unexpected, StringComparison.Ordinal))
            throw new Exception($"{message}. Did not expect '{unexpected}' in '{actual}'.");
    }

    private static string ReadRepoFile(string relativePath)
    {
        var repoRoot = FindRepoRoot();
        return File.ReadAllText(Path.Combine(repoRoot, relativePath));
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MainWindow.xaml")) &&
                File.Exists(Path.Combine(current.FullName, "CodexEnvironmentManager.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new Exception("Unable to locate repository root from test output directory.");
    }

    private static SessionManager CreateSessionManager(FakeBestEffortDesktopTerminator terminator)
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        var config = new ConfigService(baseDir);
        return new SessionManager(config, terminator);
    }

    private sealed class FakeBestEffortDesktopTerminator : IBestEffortDesktopTerminator
    {
        public DesktopKillAttemptResult Result { get; set; } = DesktopKillAttemptResult.Inconclusive("not configured");
        public DesktopKillAttemptResult? KillResult { get; set; }
        public BestEffortDesktopSessionInspection InspectResult { get; set; } = BestEffortDesktopSessionInspection.NoDesktop("not configured");

        public BestEffortDesktopSessionInspection InspectBestEffortDesktopSession(Session session) => InspectResult;
        public DesktopKillAttemptResult TryKillBestEffortDesktopSession(Session session) => KillResult ?? Result;
    }
}
