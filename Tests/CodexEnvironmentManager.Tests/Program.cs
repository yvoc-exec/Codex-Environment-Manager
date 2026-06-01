using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        Run(nameof(Persona_DefaultIcon_IsEmoji), Persona_DefaultIcon_IsEmoji);
        Run(nameof(Persona_JsonMissingApprovalsReviewer_UsesUserDefault), Persona_JsonMissingApprovalsReviewer_UsesUserDefault);
        Run(nameof(BuildManagedProfileValues_IncludesApprovalsReviewer), BuildManagedProfileValues_IncludesApprovalsReviewer);
        Run(nameof(DesktopLaunchPlan_DoesNotGenerateDeepLinkVariants), DesktopLaunchPlan_DoesNotGenerateDeepLinkVariants);
        Run(nameof(DesktopWorkspaceLauncher_Source_DoesNotContainProtocolActivation), DesktopWorkspaceLauncher_Source_DoesNotContainProtocolActivation);
        Run(nameof(DesktopWorkspaceLauncher_Source_DoesNotQueueAttemptWorkspaceBindingAsync), DesktopWorkspaceLauncher_Source_DoesNotQueueAttemptWorkspaceBindingAsync);
        Run(nameof(DesktopWorkspaceLauncher_Source_UsesCodexAppArgsBindingMethod), DesktopWorkspaceLauncher_Source_UsesCodexAppArgsBindingMethod);
        Run(nameof(DesktopWorkspaceLauncher_Source_DoesNotContainCodexThreadsNew), DesktopWorkspaceLauncher_Source_DoesNotContainCodexThreadsNew);
        Run(nameof(CodexProcessManager_CreateCodexAppProcessStartInfo_IncludesProfileOverride), CodexProcessManager_CreateCodexAppProcessStartInfo_IncludesProfileOverride);
        Run(nameof(DesktopWorkspaceLauncher_CreateBaseLaunchStartInfo_PassesCodexProfileName), DesktopWorkspaceLauncher_CreateBaseLaunchStartInfo_PassesCodexProfileName);
        Run(nameof(DesktopLaunchPlan_DefaultsDesktopReadbackFieldsToUnknown), DesktopLaunchPlan_DefaultsDesktopReadbackFieldsToUnknown);
        Run(nameof(DesktopLaunchPlan_PrefersCodexAppWhenCliFallbackIsAvailable), DesktopLaunchPlan_PrefersCodexAppWhenCliFallbackIsAvailable);
        Run(nameof(DesktopLaunchPlan_RecordsProfileOverride), DesktopLaunchPlan_RecordsProfileOverride);
        Run(nameof(BatchEscaping_EscapesSpecialCharacters), BatchEscaping_EscapesSpecialCharacters);
        Run(nameof(BatchTitleEscaping_EscapesSpecialCharacters), BatchTitleEscaping_EscapesSpecialCharacters);
        Run(nameof(NormalizeDesktopOverridePath_ReturnsNullForBlankInput), NormalizeDesktopOverridePath_ReturnsNullForBlankInput);
        Run(nameof(CodexProcessManager_IsWindowsAppsPath_DetectsStorePaths), CodexProcessManager_IsWindowsAppsPath_DetectsStorePaths);
        Run(nameof(CodexProcessManager_IsWindowsAppsPath_AllowsNormalPaths), CodexProcessManager_IsWindowsAppsPath_AllowsNormalPaths);
        Run(nameof(CodexProcessManager_TryFindCodexDesktopExe_RejectsWindowsAppsOverride), CodexProcessManager_TryFindCodexDesktopExe_RejectsWindowsAppsOverride);
        Run(nameof(DesktopLaunchPlan_StoreAppWithoutCli_ProducesClearFailure), DesktopLaunchPlan_StoreAppWithoutCli_ProducesClearFailure);
        Run(nameof(FirstRunWizard_BuildCompletedSettings_PreservesBlankDesktopPathAndMarksOnboardingComplete), FirstRunWizard_BuildCompletedSettings_PreservesBlankDesktopPathAndMarksOnboardingComplete);
        Run(nameof(ResolveManagedActiveProfileName_FallsBackToGeneratedProfileAndWarns), ResolveManagedActiveProfileName_FallsBackToGeneratedProfileAndWarns);
        Run(nameof(ShouldRemoveSessionAfterKill_OnlyWhenConfirmed), ShouldRemoveSessionAfterKill_OnlyWhenConfirmed);
        Run(nameof(ShouldRemoveSessionAfterKill_AuthoritativeRemovalDoesNotBypassMissingConfirmation), ShouldRemoveSessionAfterKill_AuthoritativeRemovalDoesNotBypassMissingConfirmation);
        Run(nameof(DesktopProcessTargetResolver_IdentifiesMainCodexDesktopAndIgnoresElectronChildren), DesktopProcessTargetResolver_IdentifiesMainCodexDesktopAndIgnoresElectronChildren);
        Run(nameof(DesktopProcessTargetResolver_ReturnsAmbiguousForMultipleRootDesktopProcesses), DesktopProcessTargetResolver_ReturnsAmbiguousForMultipleRootDesktopProcesses);
        Run(nameof(DesktopProcessTargetResolver_ExcludesCodexCliCompanionProcesses), DesktopProcessTargetResolver_ExcludesCodexCliCompanionProcesses);
        Run(nameof(SessionManager_DoesNotPruneDesktopStoreWhenRootDesktopProcessIsLive), SessionManager_DoesNotPruneDesktopStoreWhenRootDesktopProcessIsLive);
        Run(nameof(BestEffortDesktopKill_NoLiveDesktopWithArtifacts_IsRemovedOnKill), BestEffortDesktopKill_NoLiveDesktopWithArtifacts_IsRemovedOnKill);
        Run(nameof(BestEffortDesktopKill_FreshNoLiveDesktopWithArtifacts_IsRetainedDuringLaunchGrace), BestEffortDesktopKill_FreshNoLiveDesktopWithArtifacts_IsRetainedDuringLaunchGrace);
        Run(nameof(BestEffortDesktopKill_ConfirmedResolutionRemovesOnlyDesktopSession), BestEffortDesktopKill_ConfirmedResolutionRemovesOnlyDesktopSession);
        Run(nameof(BestEffortDesktopKill_AmbiguousDesktopCandidates_KeepsSessionVisible), BestEffortDesktopKill_AmbiguousDesktopCandidates_KeepsSessionVisible);
        Run(nameof(BestEffortDesktopKill_AmbiguousLiveDesktop_KeepsSessionVisible), BestEffortDesktopKill_AmbiguousLiveDesktop_KeepsSessionVisible);
        Run(nameof(RegisterDesktopSession_CreatesArtifactsForDesktopStoreSessions), RegisterDesktopSession_CreatesArtifactsForDesktopStoreSessions);
        Run(nameof(RecoverDesktopLaunchAfterFailure_UsesPreSwapWordingWhenJunctionWasNeverSwapped), RecoverDesktopLaunchAfterFailure_UsesPreSwapWordingWhenJunctionWasNeverSwapped);
        Run(nameof(SessionManager_Constructor_RequiresDesktopTerminator), SessionManager_Constructor_RequiresDesktopTerminator);
        Run(nameof(LauncherService_BlocksUnmanagedDesktopLaunches), LauncherService_BlocksUnmanagedDesktopLaunches);
        Run(nameof(MainWindow_CompositionRoot_WiresProcessManagerIntoSessionManager), MainWindow_CompositionRoot_WiresProcessManagerIntoSessionManager);
        Run(nameof(MainWindow_Xaml_ReplacesMojibakeGlyphsWithSafeLabels), MainWindow_Xaml_ReplacesMojibakeGlyphsWithSafeLabels);
        Run(nameof(TrayService_Source_UsesAppIconAssetInsteadOfSystemIcon), TrayService_Source_UsesAppIconAssetInsteadOfSystemIcon);
        Run(nameof(Project_EmbedsAppIconAsset), Project_EmbedsAppIconAsset);
        Run(nameof(DesktopLaunchRollbackTarget_ReturnsPreviousManagedAccountWhenDifferent), DesktopLaunchRollbackTarget_ReturnsPreviousManagedAccountWhenDifferent);
        Run(nameof(DesktopLaunchRollbackTarget_ReturnsNullForMissingOrSameAccount), DesktopLaunchRollbackTarget_ReturnsNullForMissingOrSameAccount);
        Run(nameof(DesktopRecoverySession_IsReliableOnlyForRealDesktopSessions), DesktopRecoverySession_IsReliableOnlyForRealDesktopSessions);
        Run(nameof(BuildDesktopLaunchRecoveryResult_ReportsRestoreAndRelaunchWithoutClaimingSuccess), BuildDesktopLaunchRecoveryResult_ReportsRestoreAndRelaunchWithoutClaimingSuccess);
        Run(nameof(BuildDesktopLaunchRecoveryResult_RequiresManualRelaunchWhenRecoveryIsUntrusted), BuildDesktopLaunchRecoveryResult_RequiresManualRelaunchWhenRecoveryIsUntrusted);
        Run(nameof(SessionManager_PruneExitedSessions_RemovesClearlyDeadDesktopStoreSessionsWithArtifacts), SessionManager_PruneExitedSessions_RemovesClearlyDeadDesktopStoreSessionsWithArtifacts);
        Run(nameof(SessionManager_PruneExitedSessions_RetainsFreshDesktopStoreSessionsDuringLaunchGrace), SessionManager_PruneExitedSessions_RetainsFreshDesktopStoreSessionsDuringLaunchGrace);
        Run(nameof(SessionManager_LiveTrackedPidSession_IsRetained), SessionManager_LiveTrackedPidSession_IsRetained);
        Run(nameof(IsBestEffortDesktopSession_MatchesOnlyDesktopStorePlaceholderCases), IsBestEffortDesktopSession_MatchesOnlyDesktopStorePlaceholderCases);
        Run(nameof(SessionViewModel_TypeIcon_TreatsDesktopStoreAsDesktop), SessionViewModel_TypeIcon_TreatsDesktopStoreAsDesktop);
        Run(nameof(DesktopSession_Status_DoesNotClaimVerifiedProfileWithoutEvidence), DesktopSession_Status_DoesNotClaimVerifiedProfileWithoutEvidence);
        Run(nameof(Session_DisplayName_TreatsKimiCliSessionsAsKimiWorkspace), Session_DisplayName_TreatsKimiCliSessionsAsKimiWorkspace);
        Run(nameof(Persona_IsKimiModel_DetectsProvider), Persona_IsKimiModel_DetectsProvider);
        Run(nameof(KimiAgentFileBuilder_WritesConservativeSessionArtifacts), KimiAgentFileBuilder_WritesConservativeSessionArtifacts);
        Run(nameof(KimiCliManager_Source_UsesTerminalLoginAndAgentFile), KimiCliManager_Source_UsesTerminalLoginAndAgentFile);
        Run(nameof(KimiCliManager_Source_UsesKimiApiKeyEnvVar), KimiCliManager_Source_UsesKimiApiKeyEnvVar);
        Run(nameof(KimiCliManager_Source_LoginFallbackSetsKimiCodeHome), KimiCliManager_Source_LoginFallbackSetsKimiCodeHome);
        Run(nameof(LauncherService_KimiLaunch_ValidatesProviderCompatibility), LauncherService_KimiLaunch_ValidatesProviderCompatibility);
        Run(nameof(ProviderCapabilities_ExposeCodexAndKimiContracts), ProviderCapabilities_ExposeCodexAndKimiContracts);
        Run(nameof(KimiCliManager_BuildLaunchPreview_IncludesSelectedModel), KimiCliManager_BuildLaunchPreview_IncludesSelectedModel);
        Run(nameof(Persona_KimiOptions_DefaultsAreSafe), Persona_KimiOptions_DefaultsAreSafe);
        Run(nameof(Persona_KimiOptions_RoundTripThroughJson), Persona_KimiOptions_RoundTripThroughJson);
        Run(nameof(KimiPersonaMigration_SafeLegacyArgsAreMigratedAndRemoved), KimiPersonaMigration_SafeLegacyArgsAreMigratedAndRemoved);
        Run(nameof(KimiPersonaMigration_ForbiddenLegacyArgsAreBlocking), KimiPersonaMigration_ForbiddenLegacyArgsAreBlocking);
        Run(nameof(KimiPersonaMigration_LegacyAcpIsRemovedWithWarning), KimiPersonaMigration_LegacyAcpIsRemovedWithWarning);
        Run(nameof(KimiPersonaMigration_DoesNotTouchCodexProfiles), KimiPersonaMigration_DoesNotTouchCodexProfiles);
        Run(nameof(KimiCliManager_BuildLaunchPreview_IncludesKimiOptionalFlags), KimiCliManager_BuildLaunchPreview_IncludesKimiOptionalFlags);
        Run(nameof(KimiCliManager_PrepareLaunch_ValidatesKimiOptionalPaths), KimiCliManager_PrepareLaunch_ValidatesKimiOptionalPaths);
        Run(nameof(KimiCliManager_PrepareLaunch_RejectsUnsupportedKimiArgs), KimiCliManager_PrepareLaunch_RejectsUnsupportedKimiArgs);
        Run(nameof(KimiCliManager_PrepareLaunch_UsesMigratedKimiOptions), KimiCliManager_PrepareLaunch_UsesMigratedKimiOptions);
        Run(nameof(KimiCliManager_PrepareLaunch_WritesSelectedModelIntoLaunchScript), KimiCliManager_PrepareLaunch_WritesSelectedModelIntoLaunchScript);
        Run(nameof(KimiCliManager_CreateLoginStartInfo_UsesAccountSpecificKimiCodeHome), KimiCliManager_CreateLoginStartInfo_UsesAccountSpecificKimiCodeHome);
        Run(nameof(KimiCliManager_CreateLoginStartInfo_UsesKimiShareDir), KimiCliManager_CreateLoginStartInfo_UsesKimiShareDir);
        Run(nameof(KimiCliManager_PrepareLaunch_WritesKimiShareDirAndUtf8Environment), KimiCliManager_PrepareLaunch_WritesKimiShareDirAndUtf8Environment);
        Run(nameof(KimiCliManager_PrepareLaunch_UsesDirectPowerShellInvocationForInteractiveKimi), KimiCliManager_PrepareLaunch_UsesDirectPowerShellInvocationForInteractiveKimi);
        Run(nameof(LauncherService_WriteKimiLaunchPlan_IncludesSelectedModel), LauncherService_WriteKimiLaunchPlan_IncludesSelectedModel);
        Run(nameof(MainWindow_Xaml_ExposesKimiLaunchAndLogin), MainWindow_Xaml_ExposesKimiLaunchAndLogin);
        Run(nameof(MainWindow_Xaml_ExposesContextualKimiSetupButton), MainWindow_Xaml_ExposesContextualKimiSetupButton);
        Run(nameof(MainWindow_Source_RoutesLaunchesByProvider), MainWindow_Source_RoutesLaunchesByProvider);
        Run(nameof(MainWindow_Source_UsesProviderCapabilitiesForKimiSetup), MainWindow_Source_UsesProviderCapabilitiesForKimiSetup);
        Run(nameof(PersonaEditorWindow_Source_DisablesCodexOnlyControlsForKimi), PersonaEditorWindow_Source_DisablesCodexOnlyControlsForKimi);
        Run(nameof(PersonaEditorWindow_Source_ExposesKimiOptionalControls), PersonaEditorWindow_Source_ExposesKimiOptionalControls);
        Run(nameof(PersonaEditorWindow_Source_ShowsKimiMigrationWarning), PersonaEditorWindow_Source_ShowsKimiMigrationWarning);
        Run(nameof(SettingsWindow_Xaml_ExposesKimiPathSetting), SettingsWindow_Xaml_ExposesKimiPathSetting);
        Run(nameof(Account_BackwardCompatibility_MigratesMissingProviderToCodex), Account_BackwardCompatibility_MigratesMissingProviderToCodex);
        Run(nameof(Account_KimiProvider_DerivesIsolatedKimiHome), Account_KimiProvider_DerivesIsolatedKimiHome);
        Run(nameof(Account_KimiAndCodexAccounts_HaveDifferentIcons), Account_KimiAndCodexAccounts_HaveDifferentIcons);
        Run(nameof(LauncherService_ValidateProviderCompatibility_BlocksMismatch), LauncherService_ValidateProviderCompatibility_BlocksMismatch);
        Run(nameof(SessionManager_StaleNullPidSession_WithNoLiveEvidence_IsPruned), SessionManager_StaleNullPidSession_WithNoLiveEvidence_IsPruned);
        Run(nameof(SessionManager_KillSession_AlreadyDeadTrackedPid_RetiresStaleRow), SessionManager_KillSession_AlreadyDeadTrackedPid_RetiresStaleRow);
        Run(nameof(SessionManager_StaleSession_AmbiguousRecentSession_IsRetained), SessionManager_StaleSession_AmbiguousRecentSession_IsRetained);
        Run(nameof(KimiCliManager_AccountAwareLogin_SetsKimiCodeHome), KimiCliManager_AccountAwareLogin_SetsKimiCodeHome);
        Run(nameof(SettingsWindow_Xaml_RemovesGlobalKimiLoginButton), SettingsWindow_Xaml_RemovesGlobalKimiLoginButton);
        Run(nameof(AccountWizardWindow_Source_SupportsCodexAndKimi), AccountWizardWindow_Source_SupportsCodexAndKimi);
        Run(nameof(LauncherService_FiltersStaleConfigProfileOverride), LauncherService_FiltersStaleConfigProfileOverride);
        Run(nameof(LauncherService_PreservesNonProfileConfigOverride), LauncherService_PreservesNonProfileConfigOverride);
        Run(nameof(ConfigService_StripsStalePersistedConfigProfileOverride), ConfigService_StripsStalePersistedConfigProfileOverride);
        Run(nameof(ConfigService_PreservesSafePersistedConfigOverride), ConfigService_PreservesSafePersistedConfigOverride);

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

    private static void Persona_DefaultIcon_IsEmoji()
    {
        var persona = new Persona();
        AssertEqual("👤", persona.Icon, "new personas should default to the user emoji icon");
    }

    private static void Persona_JsonMissingApprovalsReviewer_UsesUserDefault()
    {
        var persona = JsonSerializer.Deserialize<Persona>("{}") ?? throw new Exception("persona did not deserialize");
        AssertEqual("user", persona.ApprovalsReviewer, "deserializing old personas should keep the user default");
    }

    private static void DesktopLaunchPlan_DoesNotGenerateDeepLinkVariants()
    {
        var source = ReadRepoFile("Services/DesktopWorkspaceLauncher.cs");

        AssertNotContains("plan.DeepLinkVariants = BuildCodexWorkspaceUriVariants", source, "BuildLaunchPlan should no longer generate deep-link variants");
        AssertNotContains("BuildCodexWorkspaceUriVariants", source, "DesktopWorkspaceLauncher should not contain BuildCodexWorkspaceUriVariants");
        AssertNotContains("BuildCodexWorkspaceUri(", source, "DesktopWorkspaceLauncher should not contain BuildCodexWorkspaceUri");
    }

    private static void DesktopWorkspaceLauncher_Source_DoesNotContainProtocolActivation()
    {
        var source = ReadRepoFile("Services/DesktopWorkspaceLauncher.cs");

        AssertNotContains("ProcessStartInfo.FileName = variant.ActivationUri", source, "protocol activation via variant.ActivationUri must be removed");
        AssertNotContains("Attempting ActivationSucceeded handoff", source, "deep-link handoff log must be removed");
        AssertNotContains("Protocol activation request succeeded", source, "protocol success log must be removed");
        AssertNotContains("ActivationSucceeded recorded", source, "ActivationSucceeded log must be removed");
    }

    private static void DesktopWorkspaceLauncher_Source_DoesNotQueueAttemptWorkspaceBindingAsync()
    {
        var source = ReadRepoFile("Services/DesktopWorkspaceLauncher.cs");

        AssertNotContains("Task.Run(() => AttemptWorkspaceBindingAsync", source, "post-launch protocol binding task must be removed");
        AssertNotContains("AttemptWorkspaceBindingAsync", source, "AttemptWorkspaceBindingAsync method must be removed");
    }

    private static void DesktopWorkspaceLauncher_Source_UsesCodexAppArgsBindingMethod()
    {
        var source = ReadRepoFile("Services/DesktopWorkspaceLauncher.cs");

        AssertContains("codex_app_args", source, "codex_app launch should record codex_app_args binding method");
        AssertContains("workspace path is already passed as CLI argument", source, "codex_app launch should document why binding is skipped");
    }

    private static void DesktopWorkspaceLauncher_Source_DoesNotContainCodexThreadsNew()
    {
        var source = ReadRepoFile("Services/DesktopWorkspaceLauncher.cs");

        AssertNotContains("codex://threads/new", source, "no runtime code should construct codex://threads/new URIs");
    }

    private static void CodexProcessManager_CreateCodexAppProcessStartInfo_IncludesProfileOverride()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var fakeCodexPath = Path.Combine(tempDir, "codex.exe");
        File.WriteAllText(fakeCodexPath, string.Empty);
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            Environment.SetEnvironmentVariable("PATH", tempDir + Path.PathSeparator + (originalPath ?? string.Empty));
            var log = new LogService();
            var manager = new CodexProcessManager(log);
            var method = typeof(CodexProcessManager).GetMethod(
                "CreateCodexAppProcessStartInfo",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(string), typeof(string) },
                null);

            if (method == null)
                throw new Exception("CreateCodexAppProcessStartInfo should accept a profileName override for Desktop launches.");

            var psi = (ProcessStartInfo)method.Invoke(manager, new object[] { @"E:\Cem", @"D:\Work\Repo", "cem_planner_reviewer_gpt_5_5_high_9b360c9f" })!;
            var args = string.Join(" ", psi.ArgumentList.ToArray());

            AssertEqual(fakeCodexPath, psi.FileName, "codex app should use the discovered CLI executable");
            AssertContains("app", args, "codex app launch should include the app subcommand");
            AssertContains("--profile", args, "codex app launch should pass the profile override");
            AssertContains("cem_planner_reviewer_gpt_5_5_high_9b360c9f", args, "codex app launch should explicitly select the requested Codex profile");
            AssertContains(@"D:\Work\Repo", args, "codex app launch should include the workspace path");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static void DesktopWorkspaceLauncher_CreateBaseLaunchStartInfo_PassesCodexProfileName()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var fakeCodexPath = Path.Combine(tempDir, "codex.exe");
        File.WriteAllText(fakeCodexPath, string.Empty);
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            Environment.SetEnvironmentVariable("PATH", tempDir + Path.PathSeparator + (originalPath ?? string.Empty));

            var log = new LogService();
            var processManager = new CodexProcessManager(log);
            var launcher = new DesktopWorkspaceLauncher(processManager, log);
            var plan = new DesktopWorkspaceLaunchPlan
            {
                AccountId = "acct",
                AccountName = "Sachi-Main",
                CodexHome = @"E:\Cem",
                PersonaId = "persona",
                PersonaName = "Planner+Reviewer GPT 5.5 High",
                CodexProfileName = "cem_planner_reviewer_gpt_5_5_high_9b360c9f",
                WorkspaceId = "ws",
                WorkspaceName = "CodexManager",
                WorkspacePath = @"D:\Work\Repo",
                CliFallbackPath = fakeCodexPath,
                BaseLaunchMethod = "codex_app",
                CanLaunch = true
            };

            var psi = launcher.CreateBaseLaunchStartInfo(plan);
            var args = string.Join(" ", psi.ArgumentList.ToArray());

            AssertContains("app", args, "Desktop launch should use codex app");
            AssertContains("--profile", args, "Desktop launch should pass the profile override");
            AssertContains("cem_planner_reviewer_gpt_5_5_high_9b360c9f", args, "Desktop launch should explicitly pass the selected Codex profile");
            AssertContains(@"D:\Work\Repo", args, "Desktop launch should include the workspace path");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static void DesktopLaunchPlan_RecordsProfileOverride()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var fakeCodexPath = Path.Combine(tempDir, "codex.exe");
        File.WriteAllText(fakeCodexPath, string.Empty);
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            Environment.SetEnvironmentVariable("PATH", tempDir + Path.PathSeparator + (originalPath ?? string.Empty));

            var log = new LogService();
            var processManager = new CodexProcessManager(log);
            var launcher = new DesktopWorkspaceLauncher(processManager, log);
            var plan = new DesktopWorkspaceLaunchPlan
            {
                AccountId = "acct",
                AccountName = "Sachi-Main",
                CodexHome = @"E:\Cem",
                PersonaId = "persona",
                PersonaName = "Planner+Reviewer GPT 5.5 High",
                CodexProfileName = "cem_planner_reviewer_gpt_5_5_high_9b360c9f",
                WorkspaceId = "ws",
                WorkspaceName = "CodexManager",
                WorkspacePath = @"D:\Work\Repo",
                CliFallbackPath = fakeCodexPath,
                BaseLaunchMethod = "codex_app",
                CanLaunch = true
            };

            launcher.CreateBaseLaunchStartInfo(plan);

            var commandPreview = GetRequiredPropertyValue<string>(plan, "CommandPreview");
            var profileLaunchMethod = GetRequiredPropertyValue<string>(plan, "ProfileLaunchMethod");
            var profileVerificationStatus = GetRequiredPropertyValue<string>(plan, "ProfileVerificationStatus");
            var profileOverrideArgs = GetRequiredPropertyValue<string[]>(plan, "ProfileOverrideArgs");

            AssertContains("codex", commandPreview, "Desktop launch plan should record a command preview");
            AssertContains("--profile", commandPreview, "Desktop launch plan command preview should include the selected profile override");
            AssertContains("cem_planner_reviewer_gpt_5_5_high_9b360c9f", commandPreview, "Desktop launch plan command preview should include the selected profile name");
            AssertContains("codex app", profileLaunchMethod, "Desktop launch plan should record the profile-controlled launch method");
            AssertContains("override", profileVerificationStatus.ToLowerInvariant(), "Desktop launch plan should record that the profile override was applied");
            AssertContains("--profile", string.Join(" ", profileOverrideArgs), "Desktop launch plan should record the profile override args");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static void DesktopLaunchPlan_DefaultsDesktopReadbackFieldsToUnknown()
    {
        var plan = new DesktopWorkspaceLaunchPlan();

        AssertEqual("unknown", plan.DesktopOpenedNewThread, "desktop opened new thread placeholder");
        AssertEqual("unknown", plan.ActualDesktopWorkingDirectory, "desktop working directory placeholder");
        AssertEqual("unknown", plan.WorkspaceMatchStatus, "workspace match status placeholder");
        AssertEqual<bool?>(null, plan.BindingSucceeded, "binding confirmation should default to unknown");
    }

    private static void DesktopLaunchPlan_PrefersCodexAppWhenCliFallbackIsAvailable()
    {
        var source = ReadRepoFile("Services/DesktopWorkspaceLauncher.cs");

        AssertContains("if (!string.IsNullOrWhiteSpace(detection.CliPath))", source, "Desktop launch planning should check the Codex CLI fallback first");
        AssertContains("plan.BaseLaunchMethod = \"codex_app\";", source, "Desktop launch planning should prefer codex_app when the CLI fallback is available");
    }

    private static void DesktopSession_Status_DoesNotClaimVerifiedProfileWithoutEvidence()
    {
        var source = ReadRepoFile("MainWindow.xaml.cs");

        AssertContains("ProfileVerificationStatus", source, "desktop session refresh should consult the recorded profile verification state");
        AssertContains("profile unverified", source.ToLowerInvariant(), "desktop session status should be able to show unverified profile launches");
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

    private static void CodexProcessManager_IsWindowsAppsPath_DetectsStorePaths()
    {
        AssertEqual(true, CodexProcessManager.IsWindowsAppsPath(@"C:\Program Files\WindowsApps\OpenAI.Codex_1.0.0_x64__8wekyb3d8bbwe\Codex.exe"), "typical WindowsApps path should be detected");
        AssertEqual(true, CodexProcessManager.IsWindowsAppsPath(@"C:\Program Files\WindowsApps\Codex.exe"), "short WindowsApps path should be detected");
        AssertEqual(true, CodexProcessManager.IsWindowsAppsPath(@"c:\program files\windowsapps\codex.exe"), "lowercase WindowsApps path should be detected");
        AssertEqual(true, CodexProcessManager.IsWindowsAppsPath(@"D:\SomeFolder\WindowsApps\Codex.exe"), "WindowsApps segment anywhere in path should be detected");
    }

    private static void CodexProcessManager_IsWindowsAppsPath_AllowsNormalPaths()
    {
        AssertEqual(false, CodexProcessManager.IsWindowsAppsPath(@"C:\Program Files\Codex\Codex.exe"), "normal Program Files path should not be flagged");
        AssertEqual(false, CodexProcessManager.IsWindowsAppsPath(@"D:\Codex\Codex.exe"), "custom install path should not be flagged");
        AssertEqual(false, CodexProcessManager.IsWindowsAppsPath(@"C:\Users\Alice\AppData\Local\Programs\Codex\Codex.exe"), "local app data path should not be flagged");
        AssertEqual(false, CodexProcessManager.IsWindowsAppsPath(null), "null path should not be flagged");
        AssertEqual(false, CodexProcessManager.IsWindowsAppsPath(""), "empty path should not be flagged");
        AssertEqual(false, CodexProcessManager.IsWindowsAppsPath("   "), "whitespace path should not be flagged");
    }

    private static void CodexProcessManager_TryFindCodexDesktopExe_RejectsWindowsAppsOverride()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        var windowsAppsDir = Path.Combine(tempDir, "WindowsApps");
        Directory.CreateDirectory(windowsAppsDir);
        var fakeExe = Path.Combine(windowsAppsDir, "Codex.exe");
        File.WriteAllText(fakeExe, string.Empty);

        try
        {
            var log = new LogService();
            var manager = new CodexProcessManager(log) { OverridePath = fakeExe };
            var found = manager.TryFindCodexDesktopExe(out var path);

            AssertEqual(false, found, "WindowsApps override path should be rejected");
            AssertEqual<string?>(null, path, "rejected WindowsApps override should return null path");
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static void DesktopLaunchPlan_StoreAppWithoutCli_ProducesClearFailure()
    {
        var source = ReadRepoFile("Services/DesktopWorkspaceLauncher.cs");

        AssertContains("detection.HasStoreApp", source, "Desktop launch planning should check for Store app when no CLI or exe is available");
        AssertContains("Microsoft Store Codex was detected, but Codex CLI was not found", source, "Store-only detection should produce a specific failure reason guiding the user to install CLI");
        AssertContains("codex app", source, "Store-only failure message should mention the codex app fallback");
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

    private static void DesktopProcessTargetResolver_IdentifiesMainCodexDesktopAndIgnoresElectronChildren()
    {
        var target = DesktopProcessTargetResolver.Resolve(new[]
        {
            new CodexProcessSnapshot(101, "Codex.exe", @"C:\Program Files\WindowsApps\OpenAI.Codex_26.519.5221.0_x64__2p2nqsd0c76g0\app\Codex.exe codex://threads/new?path=D%3A%5COwn%20Projects%5CAI%5CCodexEnvironmentManager"),
            new CodexProcessSnapshot(202, "Codex.exe", @"C:\Program Files\WindowsApps\OpenAI.Codex_26.519.5221.0_x64__2p2nqsd0c76g0\app\Codex.exe --type=crashpad-handler --user-data-dir=C:\Users\user\AppData\Local\OpenAI"),
            new CodexProcessSnapshot(203, "Codex.exe", @"C:\Program Files\WindowsApps\OpenAI.Codex_26.519.5221.0_x64__2p2nqsd0c76g0\app\Codex.exe --type=gpu-process --user-data-dir=C:\Users\user\AppData\Local\OpenAI"),
            new CodexProcessSnapshot(204, "Codex.exe", @"C:\Program Files\WindowsApps\OpenAI.Codex_26.519.5221.0_x64__2p2nqsd0c76g0\app\Codex.exe --type=utility --utility-sub-type=network.mojom.NetworkService"),
            new CodexProcessSnapshot(205, "Codex.exe", @"C:\Program Files\WindowsApps\OpenAI.Codex_26.519.5221.0_x64__2p2nqsd0c76g0\app\Codex.exe --type=renderer --user-data-dir=C:\Users\user\AppData\Local\OpenAI"),
            new CodexProcessSnapshot(206, "codex.exe", @"C:\Users\user\AppData\Roaming\npm\codex.exe app-server --port 3845"),
            new CodexProcessSnapshot(207, "CodexEnvironmentManager.exe", @"D:\Own Projects\AI\CodexEnvironmentManager\bin\Debug\net8.0-windows\CodexEnvironmentManager.exe")
        });

        AssertEqual(101, target?.ProcessId ?? -1, "desktop target resolver should choose the standalone Desktop root process instead of helpers or CLI companions");
    }

    private static void DesktopProcessTargetResolver_ReturnsAmbiguousForMultipleRootDesktopProcesses()
    {
        var target = DesktopProcessTargetResolver.Resolve(new[]
        {
            new CodexProcessSnapshot(101, "Codex.exe", @"C:\Program Files\WindowsApps\OpenAI.Codex_26.519.5221.0_x64__2p2nqsd0c76g0\app\Codex.exe codex://threads/new?path=D%3A%5CWork%5COne"),
            new CodexProcessSnapshot(102, "Codex.exe", @"C:\Program Files\WindowsApps\OpenAI.Codex_26.519.5221.0_x64__2p2nqsd0c76g0\app\Codex.exe codex://threads/new?path=D%3A%5CWork%5CTwo")
        });

        AssertEqual<DesktopProcessTarget?>(null, target, "multiple real desktop roots should remain ambiguous and not trigger a process kill");
    }

    private static void DesktopProcessTargetResolver_ExcludesCodexCliCompanionProcesses()
    {
        var target = DesktopProcessTargetResolver.Resolve(new[]
        {
            new CodexProcessSnapshot(301, "Codex.exe", @"C:\Users\user\AppData\Roaming\npm\codex.cmd --cd D:\work --profile implementor")
        });

        AssertEqual<DesktopProcessTarget?>(null, target, "CLI companion processes should not be treated as the desktop target");
    }

    private static void SessionManager_DoesNotPruneDesktopStoreWhenRootDesktopProcessIsLive()
    {
        var sessionManager = CreateSessionManager(new FakeBestEffortDesktopTerminator
        {
            InspectResult = BestEffortDesktopSessionInspection.Unique(
                new DesktopProcessTarget(4242, "Codex.exe", @"C:\Program Files\WindowsApps\OpenAI.Codex_26.519.5221.0_x64__2p2nqsd0c76g0\app\Codex.exe codex://threads/new?path=D%3A%5CWork"),
                "unique live desktop root detected")
        });

        var session = new Session
        {
            Id = "desktop-store-live-root-001",
            AccountId = "acct-a",
            PersonaId = "persona-a",
            WorkspaceId = "ws-a",
            Type = "desktop_store",
            IsBestEffortUntracked = true,
            StartedMarkerPath = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"), "started.txt"),
            StartTime = DateTime.Now.AddMinutes(-5)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(session.StartedMarkerPath)!);
        File.WriteAllText(session.StartedMarkerPath, DateTime.Now.ToString("O"));

        sessionManager.Register(session);
        sessionManager.PruneExitedSessions();

        AssertEqual(1, sessionManager.Active.Count, "live desktop root should keep the desktop_store session visible");
    }

    private static void BestEffortDesktopKill_NoLiveDesktopWithArtifacts_IsRemovedOnKill()
    {
        var sessionManager = CreateSessionManager(new FakeBestEffortDesktopTerminator
        {
            InspectResult = BestEffortDesktopSessionInspection.NoDesktop("no live desktop detected")
        });

        var session = new Session
        {
            AccountId = "acct-a",
            PersonaId = "persona-a",
            WorkspaceId = "ws-a",
            Type = "desktop_store",
            IsBestEffortUntracked = true,
            StartedMarkerPath = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"), "started.txt"),
            ExitMarkerPath = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"), "exit.txt"),
            StopMarkerPath = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"), "stop.txt"),
            StartTime = DateTime.Now.AddMinutes(-5)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(session.StartedMarkerPath)!);
        File.WriteAllText(session.StartedMarkerPath, DateTime.Now.ToString("O"));
        Directory.CreateDirectory(Path.GetDirectoryName(session.ExitMarkerPath)!);
        File.WriteAllText(session.ExitMarkerPath, "");
        Directory.CreateDirectory(Path.GetDirectoryName(session.StopMarkerPath)!);
        File.WriteAllText(session.StopMarkerPath, "");
        sessionManager.Register(session);

        var result = sessionManager.KillSession(session.Id);

        AssertEqual(true, result.KillConfirmed, "dead desktop session should be retired as confirmed");
        AssertEqual(true, result.SessionRemoved, "dead desktop session should be removed");
        AssertEqual(0, sessionManager.Active.Count, "dead desktop session should not remain tracked");
        AssertContains("stale", result.Message.ToLowerInvariant(), "message should explain that the row was retired");
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

    private static void BestEffortDesktopKill_AmbiguousDesktopCandidates_KeepsSessionVisible()
    {
        var sessionManager = CreateSessionManager(new FakeBestEffortDesktopTerminator
        {
            InspectResult = BestEffortDesktopSessionInspection.Ambiguous("multiple desktop candidates"),
            KillResult = DesktopKillAttemptResult.Inconclusive("should not be used")
        });

        var session = new Session
        {
            Type = "desktop_store",
            IsBestEffortUntracked = true,
            StartedMarkerPath = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"), "started.txt"),
            StartTime = DateTime.Now.AddMinutes(-5)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(session.StartedMarkerPath)!);
        File.WriteAllText(session.StartedMarkerPath, DateTime.Now.ToString("O"));

        sessionManager.Register(session);
        var result = sessionManager.KillSession(session.Id);

        AssertEqual(false, result.SessionRemoved, "ambiguous desktop candidates must stay visible");
        AssertEqual(false, result.KillConfirmed, "ambiguous desktop candidates must not be treated as confirmed");
        AssertEqual(1, sessionManager.Active.Count, "ambiguous desktop candidates must remain tracked");
        AssertContains("multiple desktop candidates", result.Message.ToLowerInvariant(), "message should explain the ambiguity");
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

    private static void SessionManager_PruneExitedSessions_RemovesClearlyDeadDesktopStoreSessionsWithArtifacts()
    {
        var sessionManager = CreateSessionManager(new FakeBestEffortDesktopTerminator
        {
            InspectResult = BestEffortDesktopSessionInspection.NoDesktop("no live desktop detected")
        });

        var sessionDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"), "sessions", "desktop-store-001");
        var session = new Session
        {
            Id = "desktop-store-001",
            AccountId = "acct-a",
            PersonaId = "persona-a",
            WorkspaceId = "ws-a",
            Type = "desktop_store",
            IsBestEffortUntracked = true,
            StartedMarkerPath = Path.Combine(sessionDir, "started.txt"),
            ExitMarkerPath = Path.Combine(sessionDir, "exit.txt"),
            StopMarkerPath = Path.Combine(sessionDir, "stop.txt"),
            StartTime = DateTime.Now.AddMinutes(-5)
        };
        Directory.CreateDirectory(sessionDir);
        File.WriteAllText(session.StartedMarkerPath, DateTime.Now.ToString("O"));
        File.WriteAllText(session.ExitMarkerPath, "");
        File.WriteAllText(session.StopMarkerPath, "");

        sessionManager.Register(session);
        sessionManager.PruneExitedSessions();

        AssertEqual(0, sessionManager.Active.Count, "clearly dead desktop_store sessions should be pruned even when managed artifacts remain");
    }

    private static void SessionManager_PruneExitedSessions_RetainsFreshDesktopStoreSessionsDuringLaunchGrace()
    {
        var sessionManager = CreateSessionManager(new FakeBestEffortDesktopTerminator
        {
            InspectResult = BestEffortDesktopSessionInspection.NoDesktop("no live desktop detected")
        });

        var sessionDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"), "sessions", "desktop-store-fresh-001");
        var session = new Session
        {
            Id = "desktop-store-fresh-001",
            AccountId = "acct-a",
            PersonaId = "persona-a",
            WorkspaceId = "ws-a",
            Type = "desktop_store",
            IsBestEffortUntracked = true,
            StartedMarkerPath = Path.Combine(sessionDir, "started.txt"),
            ExitMarkerPath = Path.Combine(sessionDir, "exit.txt"),
            StopMarkerPath = Path.Combine(sessionDir, "stop.txt"),
            StartTime = DateTime.Now.AddSeconds(-5)
        };
        Directory.CreateDirectory(sessionDir);
        File.WriteAllText(session.StartedMarkerPath, DateTime.Now.ToString("O"));
        File.WriteAllText(session.ExitMarkerPath, "");
        File.WriteAllText(session.StopMarkerPath, "");

        sessionManager.Register(session);
        var inspection = sessionManager.InspectSession(session);
        sessionManager.PruneExitedSessions();

        AssertEqual(SessionLiveState.Ambiguous, inspection.State, "fresh desktop_store sessions with no live desktop should remain ambiguous during launch grace");
        AssertContains("settling", inspection.Message.ToLowerInvariant(), "fresh desktop_store inspection should explain that desktop launch is still settling");
        AssertEqual(1, sessionManager.Active.Count, "fresh desktop_store sessions should not be pruned during the launch grace window");
    }

    private static void BestEffortDesktopKill_FreshNoLiveDesktopWithArtifacts_IsRetainedDuringLaunchGrace()
    {
        var sessionManager = CreateSessionManager(new FakeBestEffortDesktopTerminator
        {
            InspectResult = BestEffortDesktopSessionInspection.NoDesktop("no live desktop detected")
        });

        var session = new Session
        {
            AccountId = "acct-a",
            PersonaId = "persona-a",
            WorkspaceId = "ws-a",
            Type = "desktop_store",
            IsBestEffortUntracked = true,
            StartedMarkerPath = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"), "started.txt"),
            ExitMarkerPath = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"), "exit.txt"),
            StopMarkerPath = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"), "stop.txt"),
            StartTime = DateTime.Now.AddSeconds(-5)
        };
        Directory.CreateDirectory(Path.GetDirectoryName(session.StartedMarkerPath)!);
        File.WriteAllText(session.StartedMarkerPath, DateTime.Now.ToString("O"));
        Directory.CreateDirectory(Path.GetDirectoryName(session.ExitMarkerPath)!);
        File.WriteAllText(session.ExitMarkerPath, "");
        Directory.CreateDirectory(Path.GetDirectoryName(session.StopMarkerPath)!);
        File.WriteAllText(session.StopMarkerPath, "");
        sessionManager.Register(session);

        var result = sessionManager.KillSession(session.Id);

        AssertEqual(false, result.KillConfirmed, "fresh desktop session in launch grace should not be treated as confirmed dead");
        AssertEqual(false, result.SessionRemoved, "fresh desktop session in launch grace should remain visible");
        AssertEqual(1, sessionManager.Active.Count, "fresh desktop session in launch grace should remain tracked");
        AssertContains("settling", result.Message.ToLowerInvariant(), "kill result should explain that the desktop launch is still settling");
    }

    private static void SessionManager_LiveTrackedPidSession_IsRetained()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        var config = new ConfigService(baseDir);
        var sm = new SessionManager(config, new FakeBestEffortDesktopTerminator());

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add("Start-Sleep -Seconds 30");

        using var proc = Process.Start(psi) ?? throw new Exception("failed to start live marker-tracked helper process");
        try
        {
            System.Threading.Thread.Sleep(2000);

            var session = new Session
            {
                Id = "live-pid-001",
                Type = "cli",
                ProcessId = proc.Id,
                StartTime = DateTime.Now.AddMinutes(-1)
            };

            sm.Register(session);
            sm.PruneExitedSessions();

            AssertEqual(1, sm.Active.Count, "live tracked PID CLI sessions should be retained");
        }
        finally
        {
            try
            {
                if (!proc.HasExited)
                    proc.Kill(entireProcessTree: true);
            }
            catch
            {
                // best effort cleanup
            }
        }
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

    private static void LauncherService_BlocksUnmanagedDesktopLaunches()
    {
        var method = typeof(LauncherService).GetMethod(
            "GetUnmanagedDesktopLaunchBlockReason",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
            throw new Exception("LauncherService should expose an unmanaged Desktop detection helper before launch.");

        var liveTargets = new List<DesktopProcessTarget>
        {
            new DesktopProcessTarget(4242, "Codex.exe", "Codex.exe codex://threads/new?path=D:\\Work\\Repo")
        };

        var reason = (string?)method.Invoke(
            null,
            new object[]
            {
                Array.Empty<Session>(),
                liveTargets,
                new Func<Session, SessionInspectionResult>(_ => SessionInspectionResult.ClearlyDead("not tracked"))
            });

        AssertContains("already running outside CEM tracking", reason ?? string.Empty, "unmanaged Desktop launches should be blocked with a clear message");
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

    private static void TrayService_Source_UsesAppIconAssetInsteadOfSystemIcon()
    {
        var source = ReadRepoFile("Services/TrayService.cs");

        AssertContains("AppIcon.ico", source, "tray service should load the shared app icon asset");
        AssertContains("?? SystemIcons.Application", source, "tray service should preserve the generic icon fallback");
        AssertNotContains("Icon = SystemIcons.Application", source, "tray service should not directly assign the generic application icon");
    }

    private static void Project_EmbedsAppIconAsset()
    {
        var project = ReadRepoFile("CodexEnvironmentManager.csproj");
        var iconPath = Path.Combine(FindRepoRoot(), "Assets", "AppIcon.ico");

        AssertContains("<ApplicationIcon>Assets\\AppIcon.ico</ApplicationIcon>", project, "project should define the app icon for the executable");
        AssertContains("<Resource Include=\"Assets\\AppIcon.ico\" />", project, "project should embed the app icon as a WPF resource");
        AssertEqual(true, File.Exists(iconPath), "app icon file should exist");
        AssertEqual(true, new FileInfo(iconPath).Length > 0, "app icon file should not be empty");
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

    private static void Session_DisplayName_TreatsKimiCliSessionsAsKimiWorkspace()
    {
        var kimi = new Session
        {
            Type = "kimi-cli",
            WorkspaceName = "Repo"
        };

        AssertEqual("Kimi + Repo", kimi.DisplayName, "kimi-cli sessions should label the workspace clearly");
    }

    private static void Persona_IsKimiModel_DetectsProvider()
    {
        AssertEqual(true, Persona.IsKimiModel("kimi-k2.6"), "kimi model names should be recognized as Kimi provider models");
        AssertEqual(true, new Persona { ConfigOverrides = new Dictionary<string, string> { ["model"] = "kimi-k2.5" } }.IsKimiProvider, "persona model overrides should infer Kimi provider");
        AssertEqual(false, Persona.IsKimiModel("gpt-5.4"), "Codex model names should not be treated as Kimi provider models");
    }

    private static void KimiAgentFileBuilder_WritesConservativeSessionArtifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), "cem-kimi-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var templateRoot = Path.Combine(root, "templates");
            Directory.CreateDirectory(templateRoot);
            var templatePath = Path.Combine(templateRoot, "role.md");
            File.WriteAllText(templatePath, "# Custom Role" + Environment.NewLine + "Use the safe test role.");

            var files = KimiAgentFileBuilder.CreateSessionFiles(root, "session123", "Implementor GPT 5.4 Mini High", "Repo", @"D:\Repo", templatePath);

            AssertEqual(true, File.Exists(files.AgentFilePath), "kimi agent file should be created");
            AssertEqual(true, File.Exists(files.PromptFilePath), "kimi system prompt should be created");

            var agentYaml = File.ReadAllText(files.AgentFilePath);
            var prompt = File.ReadAllText(files.PromptFilePath);

            AssertContains("extend: default", agentYaml, "kimi agent should inherit the default built-in agent");
            AssertContains("system_prompt_path: ./kimi-system.md", agentYaml, "kimi agent should point to the generated session prompt");
            AssertContains("Selected CEM profile: Implementor GPT 5.4 Mini High", prompt, "kimi prompt should capture the selected CEM profile name");
            AssertContains("Workspace path: D:\\Repo", prompt, "kimi prompt should capture the workspace path");
            AssertContains("## Role Template", prompt, "kimi prompt should include the selected role template when one is provided");
            AssertContains("Use the safe test role.", prompt, "kimi prompt should include the selected role template contents");
            AssertNotContains("CODEX_HOME", prompt, "kimi prompt should avoid Codex-specific runtime mechanics");
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static void KimiCliManager_Source_UsesTerminalLoginAndAgentFile()
    {
        var source = ReadRepoFile("Services/KimiCliManager.cs");

        AssertContains("Kimi Login / Setup", source, "Kimi login should open in a visible terminal tab");
        AssertContains("BuildKimiBatchInvocation(kimiPath, new[] { \"login\" })", source, "Kimi login should use the resolved CLI command");
        AssertContains("cmd.exe", source, "Kimi login should fall back to a visible cmd terminal");
        AssertContains("CodexEnvironmentManager", source, "Kimi terminal grouping should reuse the stable named window strategy");
        AssertContains("BuildKimiLaunchArgs", source, "Kimi launches should use the dedicated Kimi launch-arg builder");
        AssertContains("NormalizeThinkingMode", source, "Kimi launches should validate thinking mode");
        AssertContains("--thinking", source, "Kimi launches should support thinking mode");
        AssertContains("--no-thinking", source, "Kimi launches should support no-thinking mode");
        AssertContains("--plan", source, "Kimi launches should support plan mode");
        AssertContains("--skills-dir", source, "Kimi launches should support repeated skills directories");
        AssertContains("--mcp-config-file", source, "Kimi launches should support MCP config files");
        AssertContains("--add-dir", source, "Kimi launches should support repeated additional workspace dirs");
        AssertContains("RoleTemplatePath", source, "Kimi launch setup should track the selected role template");
        AssertContains("--work-dir", source, "Kimi launches should pass --work-dir");
        AssertContains("--agent-file", source, "Kimi launches should pass --agent-file");
    }

    private static void KimiCliManager_Source_UsesKimiApiKeyEnvVar()
    {
        var source = ReadRepoFile("Services/KimiCliManager.cs");

        AssertContains("KIMI_API_KEY", source, "Kimi API-key launch should use the documented KIMI_API_KEY environment variable");
        AssertNotContains("MOONSHOT_API_KEY", source, "Kimi API-key launch should not use the incorrect MOONSHOT_API_KEY env var");
    }

    private static void KimiCliManager_Source_LoginFallbackSetsKimiCodeHome()
    {
        var source = ReadRepoFile("Services/KimiCliManager.cs");

        // The fallback cmd.exe path must also set KIMI_CODE_HOME, not just the Windows Terminal path.
        AssertContains("fallbackPsi.EnvironmentVariables[\"KIMI_CODE_HOME\"] = kimiHome", source, "Kimi login fallback cmd path must set KIMI_CODE_HOME");
    }

    private static void LauncherService_KimiLaunch_ValidatesProviderCompatibility()
    {
        var source = ReadRepoFile("Services/LauncherService.cs");

        AssertContains("ValidateKimiLaunchInputs(acct, persona, ws)", source, "Kimi launch should validate inputs with account and persona");
        AssertContains("ValidateProviderCompatibility(acct, persona)", source, "Kimi launch should enforce provider compatibility");
    }

    private static void ProviderCapabilities_ExposeCodexAndKimiContracts()
    {
        var codex = ProviderCapabilities.ForProvider("codex");
        var kimi = ProviderCapabilities.ForProvider("kimi");

        AssertEqual(true, codex.SupportsDesktop, "Codex should support desktop launches");
        AssertEqual(true, codex.SupportsCli, "Codex should support CLI launches");
        AssertEqual(true, codex.SupportsApiKey, "Codex should support API-key auth");
        AssertEqual(true, codex.SupportsOauth, "Codex should support OAuth auth");
        AssertEqual("model_instructions_file", codex.ProfileMechanism, "Codex should use model_instructions_file profiles");

        AssertEqual(false, kimi.SupportsDesktop, "Kimi should not support desktop launches");
        AssertEqual(true, kimi.SupportsCli, "Kimi should support CLI launches");
        AssertEqual(true, kimi.SupportsApiKey, "Kimi should support Moonshot API-key auth");
        AssertEqual(true, kimi.SupportsOauth, "Kimi should support OAuth auth");
        AssertEqual("agent_file", kimi.ProfileMechanism, "Kimi should use agent_file profiles");
        AssertEqual(true, kimi.SupportsThinkingMode, "Kimi should support thinking mode");
        AssertEqual(true, kimi.SupportsPlanMode, "Kimi should support plan mode");
        AssertEqual(true, kimi.SupportsSkillsDir, "Kimi should support skills directories");
        AssertEqual(true, kimi.SupportsMcpConfig, "Kimi should support MCP config files");
        AssertEqual(true, kimi.SupportsAdditionalWorkspaceDirs, "Kimi should support additional workspace directories");
    }

    private static void KimiCliManager_BuildLaunchPreview_IncludesSelectedModel()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        var workspacePath = Path.Combine(baseDir, "workspace");
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(workspacePath);

        try
        {
            var config = new ConfigService(baseDir);
            var manager = new KimiCliManager(config, new LogService());
            var acct = new Account { Id = "kimi-preview-001", Name = "Kimi Preview", Provider = "kimi", Type = "kimi_oauth" };
            var persona = new Persona { Name = "Kimi Implementor", ConfigOverrides = new() { ["model"] = "kimi-k2.6" } };
            var workspace = new Workspace { Name = "Repo", Path = workspacePath };

            var preview = manager.BuildLaunchPreview(acct, persona, workspace);

            AssertContains("Model: kimi-k2.6", preview, "Kimi preview should show the selected model");
            AssertContains("--model", preview, "Kimi preview should include the selected model flag in the CLI contract");
            AssertContains("kimi-k2.6", preview, "Kimi preview should include the selected model value in the CLI contract");
        }
        finally
        {
            try { if (Directory.Exists(workspacePath)) Directory.Delete(workspacePath, recursive: true); } catch { }
            try { Directory.Delete(baseDir, recursive: true); } catch { }
        }
    }

    private static void Persona_KimiOptions_DefaultsAreSafe()
    {
        var persona = new Persona();

        AssertEqual("default", persona.KimiOptions.ThinkingMode, "new personas should default Kimi thinking mode to default");
        AssertEqual(false, persona.KimiOptions.PlanMode, "new personas should default Kimi plan mode off");
        AssertEqual(0, persona.KimiOptions.SkillsDirs.Count, "new personas should have no Kimi skills directories by default");
        AssertEqual(string.Empty, persona.KimiOptions.McpConfigFile, "new personas should have no Kimi MCP config by default");
        AssertEqual(0, persona.KimiOptions.AdditionalDirs.Count, "new personas should have no additional Kimi workspace dirs by default");
    }

    private static void Persona_KimiOptions_RoundTripThroughJson()
    {
        var persona = new Persona
        {
            Name = "Kimi Roundtrip",
            ConfigOverrides = new() { ["model"] = "kimi-k2.6" },
            KimiOptions = new KimiProfileOptions
            {
                ThinkingMode = "no-thinking",
                PlanMode = true,
                SkillsDirs = new List<string> { @"D:\skills-a", @"D:\skills-b" },
                McpConfigFile = @"D:\mcp.json",
                AdditionalDirs = new List<string> { @"D:\workspace-a" }
            }
        };

        var json = JsonSerializer.Serialize(persona);
        var roundTrip = JsonSerializer.Deserialize<Persona>(json);
        if (roundTrip == null) throw new Exception("JSON roundtrip should deserialize Persona");

        AssertEqual("no-thinking", roundTrip.KimiOptions.ThinkingMode, "Kimi thinking mode should round-trip through JSON");
        AssertEqual(true, roundTrip.KimiOptions.PlanMode, "Kimi plan mode should round-trip through JSON");
        AssertEqual(2, roundTrip.KimiOptions.SkillsDirs.Count, "Kimi skills directories should round-trip through JSON");
        AssertEqual(@"D:\mcp.json", roundTrip.KimiOptions.McpConfigFile, "Kimi MCP config file should round-trip through JSON");
        AssertEqual(1, roundTrip.KimiOptions.AdditionalDirs.Count, "Kimi additional workspace dirs should round-trip through JSON");
    }

    private static void KimiPersonaMigration_ThinkingFlagsAreMigratedAndRemoved()
    {
        var thinkingPersona = new Persona
        {
            ConfigOverrides = new() { ["model"] = "kimi-k2.6" },
            CliArgs = new List<string> { "--thinking" }
        };

        var thinkingResult = KimiPersonaMigration.Normalize(thinkingPersona);
        AssertEqual("thinking", thinkingPersona.KimiOptions.ThinkingMode, "legacy --thinking should migrate into Kimi thinking mode");
        AssertEqual(0, thinkingPersona.CliArgs.Count, "migrated legacy Kimi args should be removed from CliArgs");
        AssertEqual(false, thinkingResult.HasBlockingIssues, "migrating --thinking should not create blocking issues");

        var noThinkingPersona = new Persona
        {
            ConfigOverrides = new() { ["model"] = "kimi-k2.6" },
            CliArgs = new List<string> { "--no-thinking" }
        };

        var noThinkingResult = KimiPersonaMigration.Normalize(noThinkingPersona);
        AssertEqual("no-thinking", noThinkingPersona.KimiOptions.ThinkingMode, "legacy --no-thinking should migrate into Kimi thinking mode");
        AssertEqual(0, noThinkingPersona.CliArgs.Count, "migrated legacy Kimi args should be removed from CliArgs");
        AssertEqual(false, noThinkingResult.HasBlockingIssues, "migrating --no-thinking should not create blocking issues");
    }

    private static void KimiPersonaMigration_SafeLegacyArgsAreMigratedAndRemoved()
    {
        var skillsA = @"C:\skills-a";
        var skillsB = @"C:\skills-b";
        var addA = @"C:\workspace-a";
        var addB = @"C:\workspace-b";
        var mcp = @"C:\mcp.json";
        var persona = new Persona
        {
            ConfigOverrides = new() { ["model"] = "kimi-k2.6" },
            CliArgs = new List<string>
            {
                "--plan",
                "--skills-dir", skillsA,
                "--skills-dir", skillsB,
                "--mcp-config-file", mcp,
                "--add-dir", addA,
                "--add-dir", addB
            }
        };

        var result = KimiPersonaMigration.Normalize(persona);

        AssertEqual(true, persona.KimiOptions.PlanMode, "legacy --plan should migrate into Kimi plan mode");
        AssertEqual(2, persona.KimiOptions.SkillsDirs.Count, "legacy --skills-dir args should migrate into Kimi skills directories");
        AssertEqual(true, persona.KimiOptions.SkillsDirs.Contains(skillsA), "Kimi skills directories should include the first migrated path");
        AssertEqual(true, persona.KimiOptions.SkillsDirs.Contains(skillsB), "Kimi skills directories should include the second migrated path");
        AssertEqual(mcp, persona.KimiOptions.McpConfigFile, "legacy --mcp-config-file should migrate into Kimi MCP config");
        AssertEqual(2, persona.KimiOptions.AdditionalDirs.Count, "legacy --add-dir args should migrate into Kimi additional workspace dirs");
        AssertEqual(true, persona.KimiOptions.AdditionalDirs.Contains(addA), "Kimi additional workspace dirs should include the first migrated path");
        AssertEqual(true, persona.KimiOptions.AdditionalDirs.Contains(addB), "Kimi additional workspace dirs should include the second migrated path");
        AssertEqual(0, persona.CliArgs.Count, "migrated legacy Kimi args should be removed from CliArgs");
        AssertEqual(false, result.HasBlockingIssues, "migrating safe legacy args should not block");
    }

    private static void KimiPersonaMigration_ForbiddenLegacyArgsAreBlocking()
    {
        var persona = new Persona
        {
            ConfigOverrides = new() { ["model"] = "kimi-k2.6" },
            CliArgs = new List<string> { "--model", "bad", "--work-dir", "bad", "--agent-file", "bad", "--agent", "bad" }
        };

        var result = KimiPersonaMigration.Normalize(persona);

        AssertEqual(true, result.HasBlockingIssues, "forbidden legacy Kimi args should be blocking");
        AssertEqual(true, result.ForbiddenArgs.Any(x => x.Contains("--model", StringComparison.Ordinal)), "forbidden args should include --model");
        AssertEqual(true, result.ForbiddenArgs.Any(x => x.Contains("--work-dir", StringComparison.Ordinal)), "forbidden args should include --work-dir");
        AssertEqual(true, result.ForbiddenArgs.Any(x => x.Contains("--agent-file", StringComparison.Ordinal)), "forbidden args should include --agent-file");
        AssertEqual(true, result.ForbiddenArgs.Any(x => x.Contains("--agent", StringComparison.Ordinal)), "forbidden args should include --agent");
        AssertEqual(0, persona.CliArgs.Count, "forbidden raw args should be stripped from the stored list while remaining blocking");
    }

    private static void KimiPersonaMigration_LegacyAcpIsRemovedWithWarning()
    {
        var persona = new Persona
        {
            ConfigOverrides = new() { ["model"] = "kimi-k2.6" },
            CliArgs = new List<string> { "--acp", "login" }
        };

        var result = KimiPersonaMigration.Normalize(persona);

        AssertEqual(false, result.HasBlockingIssues, "legacy --acp should not be treated as a blocking raw arg");
        AssertEqual(true, result.Warnings.Count > 0, "legacy --acp should emit a warning");
        AssertEqual(0, persona.CliArgs.Count, "legacy --acp should be removed from CliArgs");
        AssertContains("--acp", result.Warnings[0], "legacy --acp warning should mention the deprecated flag");
    }

    private static void KimiPersonaMigration_DoesNotTouchCodexProfiles()
    {
        var cliArgs = new List<string> { "--color=always" };
        var persona = new Persona
        {
            ConfigOverrides = new() { ["model"] = "gpt-5.4" },
            CliArgs = cliArgs,
            KimiOptions = new KimiProfileOptions
            {
                ThinkingMode = "thinking",
                PlanMode = true,
                SkillsDirs = new List<string> { @"C:\skills" },
                McpConfigFile = @"C:\mcp.json",
                AdditionalDirs = new List<string> { @"C:\workspace" }
            }
        };

        var result = KimiPersonaMigration.Normalize(persona);

        AssertEqual(false, result.Changed, "Codex profiles should not be migrated by the Kimi migration helper");
        AssertEqual(1, persona.CliArgs.Count, "Codex profiles should keep their raw CLI args");
        AssertEqual("--color=always", persona.CliArgs[0], "Codex profiles should not have their raw CLI args rewritten");
        AssertEqual("thinking", persona.KimiOptions.ThinkingMode, "Codex profiles should leave Kimi options untouched");
        AssertEqual(true, persona.KimiOptions.PlanMode, "Codex profiles should leave Kimi options untouched");
    }

    private static void KimiCliManager_BuildLaunchPreview_IncludesKimiOptionalFlags()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        var workspacePath = Path.Combine(baseDir, "workspace");
        var skillsDir1 = Path.Combine(baseDir, "skills-a");
        var skillsDir2 = Path.Combine(baseDir, "skills-b");
        var addDir1 = Path.Combine(baseDir, "add-a");
        var addDir2 = Path.Combine(baseDir, "add-b");
        var mcpFile = Path.Combine(baseDir, "mcp.json");
        var fakeKimiPath = Path.Combine(baseDir, "kimi.cmd");
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(skillsDir1);
        Directory.CreateDirectory(skillsDir2);
        Directory.CreateDirectory(addDir1);
        Directory.CreateDirectory(addDir2);
        File.WriteAllText(mcpFile, "{}");
        File.WriteAllText(fakeKimiPath, "@echo off\r\necho kimi");

        try
        {
            var config = new ConfigService(baseDir);
            config.SaveList("settings", new List<AppSettings> { new AppSettings { KimiCliPath = fakeKimiPath } });
            var manager = new KimiCliManager(config, new LogService());
            var acct = new Account { Id = "kimi-preview-002", Name = "Kimi Preview", Provider = "kimi", Type = "kimi_oauth" };
            var persona = new Persona
            {
                Name = "Kimi Optional",
                ConfigOverrides = new() { ["model"] = "kimi-k2.6" },
                KimiOptions = new KimiProfileOptions
                {
                    ThinkingMode = "thinking",
                    PlanMode = true,
                    SkillsDirs = new List<string> { skillsDir1, skillsDir2 },
                    McpConfigFile = mcpFile,
                    AdditionalDirs = new List<string> { addDir1, addDir2 }
                }
            };
            var workspace = new Workspace { Name = "Repo", Path = workspacePath };

            var preview = manager.BuildLaunchPreview(acct, persona, workspace);

            var modelIndex = preview.IndexOf("--model", StringComparison.Ordinal);
            var thinkingIndex = preview.IndexOf("--thinking", StringComparison.Ordinal);
            var planIndex = preview.IndexOf("--plan", StringComparison.Ordinal);
            var skillsIndex = preview.IndexOf("--skills-dir", StringComparison.Ordinal);
            var mcpIndex = preview.IndexOf("--mcp-config-file", StringComparison.Ordinal);
            var addIndex = preview.IndexOf("--add-dir", StringComparison.Ordinal);
            var workDirIndex = preview.IndexOf("--work-dir", StringComparison.Ordinal);
            var agentFileIndex = preview.IndexOf("--agent-file", StringComparison.Ordinal);

            AssertEqual(true, modelIndex >= 0, "Kimi preview should include --model");
            AssertEqual(true, thinkingIndex >= 0, "Kimi preview should include --thinking");
            AssertEqual(true, planIndex >= 0, "Kimi preview should include --plan");
            AssertEqual(true, skillsIndex >= 0, "Kimi preview should include --skills-dir");
            AssertEqual(true, mcpIndex >= 0, "Kimi preview should include --mcp-config-file");
            AssertEqual(true, addIndex >= 0, "Kimi preview should include --add-dir");
            AssertEqual(true, workDirIndex > addIndex, "Kimi preview should place --work-dir after optional Kimi flags");
            AssertEqual(true, agentFileIndex > workDirIndex, "Kimi preview should place --agent-file after --work-dir");
            AssertEqual(2, preview.Split(new[] { "--skills-dir" }, StringSplitOptions.None).Length - 1, "Kimi preview should repeat --skills-dir for each skills directory");
            AssertEqual(2, preview.Split(new[] { "--add-dir" }, StringSplitOptions.None).Length - 1, "Kimi preview should repeat --add-dir for each additional directory");
            AssertEqual(true, preview.Contains("--skills-dir", StringComparison.Ordinal) && preview.Contains(skillsDir1, StringComparison.Ordinal), "Kimi preview should include the first skills directory");
            AssertEqual(true, preview.Contains(skillsDir2, StringComparison.Ordinal), "Kimi preview should include the second skills directory");
            AssertEqual(true, preview.Contains(mcpFile, StringComparison.Ordinal), "Kimi preview should include the MCP config file path");
            AssertEqual(true, preview.Contains(addDir1, StringComparison.Ordinal), "Kimi preview should include the first additional dir");
            AssertEqual(true, preview.Contains(addDir2, StringComparison.Ordinal), "Kimi preview should include the second additional dir");

            var noThinkingPersona = new Persona
            {
                Name = "Kimi No Thinking",
                ConfigOverrides = new() { ["model"] = "kimi-k2.6" },
                KimiOptions = new KimiProfileOptions { ThinkingMode = "no-thinking" }
            };
            var noThinkingPreview = manager.BuildLaunchPreview(acct, noThinkingPersona, workspace);
            AssertContains("--no-thinking", noThinkingPreview, "Kimi preview should include --no-thinking");
            AssertNotContains("--thinking", noThinkingPreview, "Kimi preview should not include --thinking when no-thinking is selected");

            var defaultThinkingPersona = new Persona
            {
                Name = "Kimi Default Thinking",
                ConfigOverrides = new() { ["model"] = "kimi-k2.6" },
                KimiOptions = new KimiProfileOptions { ThinkingMode = "default" }
            };
            var defaultThinkingPreview = manager.BuildLaunchPreview(acct, defaultThinkingPersona, workspace);
            AssertNotContains("--thinking", defaultThinkingPreview, "Kimi preview should omit --thinking in default mode");
            AssertNotContains("--no-thinking", defaultThinkingPreview, "Kimi preview should omit --no-thinking in default mode");
        }
        finally
        {
            try { Directory.Delete(baseDir, recursive: true); } catch { }
        }
    }

    private static void KimiCliManager_PrepareLaunch_ValidatesKimiOptionalPaths()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        var workspacePath = Path.Combine(baseDir, "workspace");
        var fakeKimiPath = Path.Combine(baseDir, "kimi.cmd");
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(workspacePath);
        File.WriteAllText(fakeKimiPath, "@echo off\r\necho kimi");

        try
        {
            var config = new ConfigService(baseDir);
            config.SaveList("settings", new List<AppSettings> { new AppSettings { KimiCliPath = fakeKimiPath } });
            var manager = new KimiCliManager(config, new LogService());
            var acct = new Account { Id = "kimi-validate-001", Name = "Kimi Validate", Provider = "kimi", Type = "kimi_oauth" };
            var persona = new Persona
            {
                Name = "Kimi Invalid",
                ConfigOverrides = new() { ["model"] = "kimi-k2.6" },
                KimiOptions = new KimiProfileOptions
                {
                    ThinkingMode = "default",
                    SkillsDirs = new List<string> { Path.Combine(baseDir, "missing-skills") },
                    McpConfigFile = Path.Combine(baseDir, "missing-mcp.json"),
                    AdditionalDirs = new List<string> { Path.Combine(baseDir, "missing-add") }
                }
            };
            var workspace = new Workspace { Name = "Repo", Path = workspacePath };

            AssertThrows<DirectoryNotFoundException>(() => manager.BuildLaunchPreview(acct, persona, workspace), "missing Kimi optional paths should block preview");
        }
        finally
        {
            try { Directory.Delete(baseDir, recursive: true); } catch { }
        }
    }

    private static void KimiCliManager_PrepareLaunch_RejectsUnsupportedKimiArgs()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        var workspacePath = Path.Combine(baseDir, "workspace");
        var fakeKimiPath = Path.Combine(baseDir, "kimi.cmd");
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(workspacePath);
        File.WriteAllText(fakeKimiPath, "@echo off\r\necho kimi");

        try
        {
            var config = new ConfigService(baseDir);
            config.SaveList("settings", new List<AppSettings> { new AppSettings { KimiCliPath = fakeKimiPath } });
            var manager = new KimiCliManager(config, new LogService());
            var acct = new Account { Id = "kimi-unsupported-001", Name = "Kimi Unsupported", Provider = "kimi", Type = "kimi_oauth" };
            var persona = new Persona
            {
                Name = "Kimi Unsupported",
                ConfigOverrides = new() { ["model"] = "kimi-k2.6" },
                CliArgs = new List<string> { "--experimental-flag", "value" }
            };
            var workspace = new Workspace { Name = "Repo", Path = workspacePath };

            AssertThrowsContains<InvalidOperationException>(() => manager.BuildLaunchPreview(acct, persona, workspace), "--experimental-flag", "unsupported Kimi args should block preview");
        }
        finally
        {
            try { Directory.Delete(baseDir, recursive: true); } catch { }
        }
    }

    private static void KimiCliManager_PrepareLaunch_UsesMigratedKimiOptions()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        var workspacePath = Path.Combine(baseDir, "workspace");
        var skillsDir = Path.Combine(baseDir, "skills");
        var addDir = Path.Combine(baseDir, "add");
        var mcpFile = Path.Combine(baseDir, "mcp.json");
        var fakeKimiPath = Path.Combine(baseDir, "kimi.cmd");
        var accountId = Guid.NewGuid().ToString("N");
        var sessionId = Guid.NewGuid().ToString("N");
        var accountPath = JunctionManager.GetAccountProfilePath(accountId);
        var sessionDir = Path.Combine(JunctionManager.SwitcherDir, "sessions", sessionId);
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(skillsDir);
        Directory.CreateDirectory(addDir);
        File.WriteAllText(mcpFile, "{}");
        Directory.CreateDirectory(accountPath);
        File.WriteAllText(fakeKimiPath, "@echo off\r\necho kimi");

        try
        {
            var config = new ConfigService(baseDir);
            config.SaveList("settings", new List<AppSettings> { new AppSettings { KimiCliPath = fakeKimiPath } });
            var manager = new KimiCliManager(config, new LogService());
            var acct = new Account { Id = accountId, Name = "Kimi Launch", Provider = "kimi", Type = "kimi_oauth" };
            var persona = new Persona
            {
                Name = "Kimi Legacy",
                ConfigOverrides = new() { ["model"] = "kimi-k2.6" },
                CliArgs = new List<string>
                {
                    "--thinking",
                    "--plan",
                    "--skills-dir", skillsDir,
                    "--mcp-config-file", mcpFile,
                    "--add-dir", addDir
                }
            };
            var workspace = new Workspace { Name = "Repo", Path = workspacePath };

            var setup = manager.PrepareLaunch(sessionId, acct, persona, workspace, preferWindowsTerminal: false);
            var wrapper = File.ReadAllText(Path.Combine(setup.SessionDirectory, "run_kimi_wrapper.ps1"));

            AssertEqual("thinking", setup.KimiOptions.ThinkingMode, "migrated legacy Kimi thinking flag should flow into launch setup");
            AssertEqual(true, setup.KimiOptions.PlanMode, "migrated legacy Kimi plan flag should flow into launch setup");
            AssertEqual(1, setup.KimiOptions.SkillsDirs.Count, "migrated legacy Kimi skills dir should flow into launch setup");
            AssertEqual(skillsDir, setup.KimiOptions.SkillsDirs[0], "migrated legacy Kimi skills dir should be preserved");
            AssertEqual(mcpFile, setup.KimiOptions.McpConfigFile, "migrated legacy Kimi MCP config should flow into launch setup");
            AssertEqual(1, setup.KimiOptions.AdditionalDirs.Count, "migrated legacy Kimi add-dir should flow into launch setup");
            AssertEqual(addDir, setup.KimiOptions.AdditionalDirs[0], "migrated legacy Kimi add-dir should be preserved");
            AssertContains("--thinking", wrapper, "Kimi wrapper should include migrated thinking mode");
            AssertContains("--plan", wrapper, "Kimi wrapper should include migrated plan mode");
            AssertContains("--skills-dir", wrapper, "Kimi wrapper should include migrated skills dir");
            AssertContains(skillsDir, wrapper, "Kimi wrapper should include the migrated skills directory path");
            AssertContains("--mcp-config-file", wrapper, "Kimi wrapper should include migrated MCP config");
            AssertContains(mcpFile, wrapper, "Kimi wrapper should include the migrated MCP config path");
            AssertContains("--add-dir", wrapper, "Kimi wrapper should include migrated additional dir");
            AssertContains(addDir, wrapper, "Kimi wrapper should include the migrated additional dir path");
        }
        finally
        {
            try { if (Directory.Exists(sessionDir)) Directory.Delete(sessionDir, recursive: true); } catch { }
            try { if (Directory.Exists(accountPath)) Directory.Delete(accountPath, recursive: true); } catch { }
            try { if (Directory.Exists(workspacePath)) Directory.Delete(workspacePath, recursive: true); } catch { }
            try { if (Directory.Exists(skillsDir)) Directory.Delete(skillsDir, recursive: true); } catch { }
            try { if (Directory.Exists(addDir)) Directory.Delete(addDir, recursive: true); } catch { }
            try { if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true); } catch { }
        }
    }

    private static void KimiCliManager_PrepareLaunch_WritesSelectedModelIntoLaunchScript()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        var workspacePath = Path.Combine(baseDir, "workspace");
        var skillsDir = Path.Combine(baseDir, "skills");
        var addDir = Path.Combine(baseDir, "add");
        var mcpFile = Path.Combine(baseDir, "mcp.json");
        var fakeKimiPath = Path.Combine(baseDir, "kimi.cmd");
        var accountId = Guid.NewGuid().ToString("N");
        var sessionId = Guid.NewGuid().ToString("N");
        var accountPath = JunctionManager.GetAccountProfilePath(accountId);
        var sessionDir = Path.Combine(JunctionManager.SwitcherDir, "sessions", sessionId);
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(skillsDir);
        Directory.CreateDirectory(addDir);
        File.WriteAllText(mcpFile, "{}");
        Directory.CreateDirectory(accountPath);
        File.WriteAllText(fakeKimiPath, "@echo off\r\necho kimi");

        try
        {
            var config = new ConfigService(baseDir);
            config.SaveList("settings", new List<AppSettings> { new AppSettings { KimiCliPath = fakeKimiPath } });
            var manager = new KimiCliManager(config, new LogService());
            var acct = new Account { Id = accountId, Name = "Kimi Launch", Provider = "kimi", Type = "kimi_oauth" };
            var persona = new Persona
            {
                Name = "Kimi Planner",
                ConfigOverrides = new() { ["model"] = "kimi-k2.6" },
                KimiOptions = new KimiProfileOptions
                {
                    ThinkingMode = "thinking",
                    PlanMode = true,
                    SkillsDirs = new List<string> { skillsDir },
                    McpConfigFile = mcpFile,
                    AdditionalDirs = new List<string> { addDir }
                }
            };
            var workspace = new Workspace { Name = "Repo", Path = workspacePath };

            var setup = manager.PrepareLaunch(sessionId, acct, persona, workspace, preferWindowsTerminal: false);
            var wrapper = File.ReadAllText(Path.Combine(setup.SessionDirectory, "run_kimi_wrapper.ps1"));

            AssertEqual(workspacePath, setup.StartInfo.WorkingDirectory, "Kimi launch should preserve the workspace working directory");
            AssertContains("--work-dir", wrapper, "Kimi wrapper should continue to pass --work-dir");
            AssertContains(workspacePath, wrapper, "Kimi wrapper should use the selected workspace path");
            AssertContains("--agent-file", wrapper, "Kimi wrapper should continue to pass --agent-file");
            AssertContains("--model", wrapper, "Kimi wrapper should include the selected model");
            AssertContains("kimi-k2.6", wrapper, "Kimi wrapper should pass the selected Kimi model");
            AssertContains("--thinking", wrapper, "Kimi wrapper should include the selected thinking mode");
            AssertContains("--plan", wrapper, "Kimi wrapper should include plan mode");
            AssertContains("--skills-dir", wrapper, "Kimi wrapper should include skills directories");
            AssertContains(skillsDir, wrapper, "Kimi wrapper should include the skills directory path");
            AssertContains("--mcp-config-file", wrapper, "Kimi wrapper should include the MCP config file");
            AssertContains(mcpFile, wrapper, "Kimi wrapper should include the MCP config file path");
            AssertContains("--add-dir", wrapper, "Kimi wrapper should include additional workspace dirs");
            AssertContains(addDir, wrapper, "Kimi wrapper should include the additional dir path");
        }
        finally
        {
            try { if (Directory.Exists(sessionDir)) Directory.Delete(sessionDir, recursive: true); } catch { }
            try { if (Directory.Exists(accountPath)) Directory.Delete(accountPath, recursive: true); } catch { }
            try { if (Directory.Exists(workspacePath)) Directory.Delete(workspacePath, recursive: true); } catch { }
            try { if (Directory.Exists(skillsDir)) Directory.Delete(skillsDir, recursive: true); } catch { }
            try { if (Directory.Exists(addDir)) Directory.Delete(addDir, recursive: true); } catch { }
            try { if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true); } catch { }
        }
    }

    private static void KimiCliManager_CreateLoginStartInfo_UsesAccountSpecificKimiCodeHome()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        var fakeKimiPath = Path.Combine(baseDir, "kimi.cmd");
        Directory.CreateDirectory(baseDir);
        File.WriteAllText(fakeKimiPath, "@echo off\r\necho kimi");

        try
        {
            var config = new ConfigService(baseDir);
            config.SaveList("settings", new List<AppSettings> { new AppSettings { KimiCliPath = fakeKimiPath } });
            var manager = new KimiCliManager(config, new LogService());
            var acct = new Account { Id = Guid.NewGuid().ToString("N"), Name = "Kimi OAuth", Provider = "kimi", Type = "kimi_oauth" };

            var startInfo = manager.CreateLoginStartInfo(acct);
            var expectedHome = KimiCliManager.GetKimiCodeHome(acct);

            AssertEqual(expectedHome, startInfo.EnvironmentVariables["KIMI_CODE_HOME"], "Kimi login should use the selected account's KIMI_CODE_HOME");
        }
        finally
        {
            try { if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true); } catch { }
        }
    }

    private static void KimiCliManager_CreateLoginStartInfo_UsesKimiShareDir()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        var fakeKimiPath = Path.Combine(baseDir, "kimi.cmd");
        Directory.CreateDirectory(baseDir);
        File.WriteAllText(fakeKimiPath, "@echo off\r\necho kimi");

        try
        {
            var config = new ConfigService(baseDir);
            config.SaveList("settings", new List<AppSettings> { new AppSettings { KimiCliPath = fakeKimiPath } });
            var manager = new KimiCliManager(config, new LogService());
            var acct = new Account { Id = Guid.NewGuid().ToString("N"), Name = "Kimi OAuth", Provider = "kimi", Type = "kimi_oauth" };

            var startInfo = manager.CreateLoginStartInfo(acct);
            var expectedHome = KimiCliManager.GetKimiCodeHome(acct);
            var joinedArgs = string.Join(" ", startInfo.ArgumentList);

            AssertEqual(expectedHome, startInfo.EnvironmentVariables["KIMI_SHARE_DIR"], "Kimi login must set KIMI_SHARE_DIR because current Kimi CLI stores runtime data under ~/.kimi by default");
            AssertContains("KIMI_SHARE_DIR", joinedArgs, "Windows Terminal login command must set KIMI_SHARE_DIR inside the spawned cmd tab");
        }
        finally
        {
            try { if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true); } catch { }
        }
    }

    private static void KimiCliManager_PrepareLaunch_WritesKimiShareDirAndUtf8Environment()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        var workspacePath = Path.Combine(baseDir, "workspace");
        var fakeKimiPath = Path.Combine(baseDir, "kimi.cmd");
        Directory.CreateDirectory(workspacePath);
        File.WriteAllText(fakeKimiPath, "@echo off\r\necho kimi");

        try
        {
            var config = new ConfigService(baseDir);
            config.SaveList("settings", new List<AppSettings> { new AppSettings { KimiCliPath = fakeKimiPath } });
            var manager = new KimiCliManager(config, new LogService());
            var acct = new Account { Id = Guid.NewGuid().ToString("N"), Name = "Kimi OAuth", Provider = "kimi", Type = "kimi_oauth" };
            var persona = new Persona { Name = "Kimi Profile" };
            persona.ConfigOverrides["model"] = "kimi-k2.6";
            var workspace = new Workspace { Name = "Workspace", Path = workspacePath };

            var setup = manager.PrepareLaunch(Guid.NewGuid().ToString("N"), acct, persona, workspace, preferWindowsTerminal: false);
            var expectedHome = KimiCliManager.GetKimiCodeHome(acct);
            var launchCmd = File.ReadAllText(setup.LaunchScriptPath);

            AssertEqual(expectedHome, setup.StartInfo.EnvironmentVariables["KIMI_SHARE_DIR"], "Kimi launch must set KIMI_SHARE_DIR on ProcessStartInfo");
            AssertContains("set \"KIMI_SHARE_DIR=", launchCmd, "Kimi launch script must set KIMI_SHARE_DIR inside the terminal tab");
            AssertContains("chcp 65001", launchCmd, "Kimi launch script must switch cmd to UTF-8 before starting the TUI");
            AssertContains("PYTHONIOENCODING=utf-8", launchCmd, "Kimi launch script must force UTF-8 Python stdio for the Windows TUI");
            AssertContains("PYTHONUTF8=1", launchCmd, "Kimi launch script must enable Python UTF-8 mode for the Windows TUI");
        }
        finally
        {
            try { if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true); } catch { }
        }
    }

    private static void KimiCliManager_PrepareLaunch_UsesDirectPowerShellInvocationForInteractiveKimi()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        var workspacePath = Path.Combine(baseDir, "workspace");
        var fakeKimiPath = Path.Combine(baseDir, "kimi.cmd");
        Directory.CreateDirectory(workspacePath);
        File.WriteAllText(fakeKimiPath, "@echo off\r\necho kimi");

        try
        {
            var config = new ConfigService(baseDir);
            config.SaveList("settings", new List<AppSettings> { new AppSettings { KimiCliPath = fakeKimiPath } });
            var manager = new KimiCliManager(config, new LogService());
            var acct = new Account { Id = Guid.NewGuid().ToString("N"), Name = "Kimi OAuth", Provider = "kimi", Type = "kimi_oauth" };
            var persona = new Persona { Name = "Kimi Profile" };
            persona.ConfigOverrides["model"] = "kimi-k2.6";
            var workspace = new Workspace { Name = "Workspace", Path = workspacePath };

            var setup = manager.PrepareLaunch(Guid.NewGuid().ToString("N"), acct, persona, workspace, preferWindowsTerminal: false);
            var wrapper = File.ReadAllText(Path.Combine(setup.SessionDirectory, "run_kimi_wrapper.ps1"));

            AssertContains("& $kimiPath @kimiArgs", wrapper, "Kimi wrapper should invoke the CLI directly so interactive TUI failures and exit codes are not hidden behind nested cmd.exe");
            AssertNotContains("Start-Process -FilePath $env:ComSpec", wrapper, "Kimi wrapper should not spawn a nested cmd.exe that can detach or hide Kimi TUI output");
        }
        finally
        {
            try { if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true); } catch { }
        }
    }

    private static void LauncherService_WriteKimiLaunchPlan_IncludesSelectedModel()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        var workspacePath = Path.Combine(baseDir, "workspace");
        var fakeKimiPath = Path.Combine(baseDir, "kimi.cmd");
        var accountId = Guid.NewGuid().ToString("N");
        var sessionId = Guid.NewGuid().ToString("N");
        var accountPath = JunctionManager.GetAccountProfilePath(accountId);
        var sessionDir = Path.Combine(JunctionManager.SwitcherDir, "sessions", sessionId);
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(accountPath);
        File.WriteAllText(fakeKimiPath, "@echo off\r\necho kimi");

        try
        {
            var config = new ConfigService(baseDir);
            config.SaveList("settings", new List<AppSettings> { new AppSettings { KimiCliPath = fakeKimiPath } });
            var log = new LogService();
            var accountManager = new AccountManager(config, log);
            var personaEngine = new PersonaEngine();
            var sessionManager = new SessionManager(config, new CodexProcessManager(log));
            var processManager = new CodexProcessManager(log);
            var desktopLauncher = new DesktopWorkspaceLauncher(processManager, log);
            var gitGuard = new GitStateGuard();
            var kimiManager = new KimiCliManager(config, log);
            var launcher = new LauncherService(accountManager, personaEngine, sessionManager, processManager, desktopLauncher, gitGuard, kimiManager, log, config);
            var acct = new Account { Id = accountId, Name = "Kimi Launch", Provider = "kimi", Type = "kimi_oauth" };
            var persona = new Persona { Name = "Kimi Planner", ConfigOverrides = new() { ["model"] = "kimi-k2.6" } };
            var workspace = new Workspace { Name = "Repo", Path = workspacePath };

            var setup = kimiManager.PrepareLaunch(sessionId, acct, persona, workspace, preferWindowsTerminal: false);
            var method = typeof(LauncherService).GetMethod("WriteKimiLaunchPlan", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method == null) throw new Exception("WriteKimiLaunchPlan method not found");
            method.Invoke(launcher, new object[] { sessionId, acct, persona, workspace, setup });

            var plan = File.ReadAllText(Path.Combine(setup.SessionDirectory, "launch_plan.json"));
            AssertContains("\"SelectedModel\": \"kimi-k2.6\"", plan, "Kimi launch plan should record the selected model");
            AssertContains("--model", plan, "Kimi launch plan should include the selected model flag in the command preview");
            AssertContains("kimi-k2.6", plan, "Kimi launch plan should include the selected model value in the command preview");
        }
        finally
        {
            try { if (Directory.Exists(sessionDir)) Directory.Delete(sessionDir, recursive: true); } catch { }
            try { if (Directory.Exists(accountPath)) Directory.Delete(accountPath, recursive: true); } catch { }
            try { if (Directory.Exists(workspacePath)) Directory.Delete(workspacePath, recursive: true); } catch { }
            try { if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true); } catch { }
        }
    }

    private static void MainWindow_Xaml_ExposesKimiLaunchAndLogin()
    {
        var xaml = ReadRepoFile("MainWindow.xaml");

        AssertNotContains("Launch Kimi Companion", xaml, "main window should not expose a separate Kimi launch button in the bottom row");
        AssertContains("Launch Desktop", xaml, "main window should keep the generic Desktop launch button");
        AssertContains("Launch CLI Companion", xaml, "main window should keep the generic CLI launch button");
        AssertContains("Add Account", xaml, "main window should expose a unified Add Account button in the Accounts panel");
        AssertNotContains("Kimi Login / Setup", xaml, "main window should no longer expose a global Kimi login/setup button");
    }

    private static void MainWindow_Xaml_ExposesContextualKimiSetupButton()
    {
        var xaml = ReadRepoFile("MainWindow.xaml");

        AssertContains("Kimi Setup", xaml, "main window should expose a contextual Kimi account setup button");
        AssertContains("KimiSetup_Click", xaml, "main window should wire the Kimi setup button to a handler");
    }

    private static void MainWindow_Source_RoutesLaunchesByProvider()
    {
        var source = ReadRepoFile("MainWindow.xaml.cs");

        AssertContains("acct.ResolvedProvider", source, "main window should branch launch behavior on the selected account provider");
        AssertContains("Desktop App not available for Kimi", source, "main window should tell the user that Kimi has no Desktop app");
        AssertContains("ConfirmKimiLaunch(acct, persona, ws)", source, "main window should use the Kimi confirmation flow for Kimi accounts");
        AssertContains("_launcher.LaunchKimiCompanion(acct, persona, ws)", source, "main window should route Kimi CLI launches to the Kimi launcher with account");
        AssertContains("_launcher.LaunchCliCompanion(acct, persona, ws)", source, "main window should continue routing Codex CLI launches to the Codex launcher");
    }

    private static void MainWindow_Source_UsesProviderCapabilitiesForKimiSetup()
    {
        var source = ReadRepoFile("MainWindow.xaml.cs");

        AssertContains("ProviderCapabilities", source, "main window should consult provider capabilities for account actions");
        AssertContains("KimiSetup_Click", source, "main window should implement a contextual Kimi setup action");
        AssertContains("RunLogin(acct)", source, "main window should launch Kimi setup through the account-scoped Kimi CLI manager");
    }

    private static void PersonaEditorWindow_Source_DisablesCodexOnlyControlsForKimi()
    {
        var xaml = ReadRepoFile("Views/PersonaEditorWindow.xaml");
        var source = ReadRepoFile("Views/PersonaEditorWindow.xaml.cs");

        AssertContains("Provider is inferred from the selected model", xaml, "profile editor should explain that provider comes from the model dropdown");
        AssertContains("x:Name=\"InstructionsRefreshButton\"", xaml, "profile editor should keep the Codex instructions refresh control named for provider-aware enabling");
        AssertContains("x:Name=\"EnvVarsBorder\"", xaml, "profile editor should keep the env vars section named for provider-aware disabling");
        AssertContains("x:Name=\"CliArgsBorder\"", xaml, "profile editor should keep the extra CLI args section named for provider-aware filtering");
        AssertContains("CliArgsBorder.Visibility = isKimi ? Visibility.Collapsed : Visibility.Visible", source, "profile editor should hide raw CLI args for Kimi");
        AssertContains("KimiOptionsBorder.Visibility = isKimi ? Visibility.Visible : Visibility.Collapsed", source, "profile editor should show Kimi options for Kimi models");
        AssertContains("UpdateProviderAwareUi", source, "profile editor should centralize provider-aware enable/disable state");
        AssertContains("ReasoningBox.IsEnabled = !isKimi", source, "profile editor should disable Codex reasoning for Kimi");
        AssertContains("SandboxBox.IsEnabled = !isKimi", source, "profile editor should disable Codex sandbox for Kimi");
        AssertContains("ApprovalBox.IsEnabled = !isKimi", source, "profile editor should disable Codex approval policy for Kimi");
        AssertContains("ApprovalsReviewerBox.IsEnabled = !isKimi", source, "profile editor should disable Codex approvals reviewer for Kimi");
        AssertContains("InstructionsRefreshButton.IsEnabled = !isKimi", source, "profile editor should disable Codex instructions refresh for Kimi");
    }

    private static void PersonaEditorWindow_Source_ExposesKimiOptionalControls()
    {
        var xaml = ReadRepoFile("Views/PersonaEditorWindow.xaml");
        var source = ReadRepoFile("Views/PersonaEditorWindow.xaml.cs");

        AssertContains("KimiOptionsBorder", xaml, "profile editor should expose a dedicated Kimi options section");
        AssertContains("ThinkingModeBox", xaml, "profile editor should expose Kimi thinking mode");
        AssertContains("PlanModeCheckBox", xaml, "profile editor should expose Kimi plan mode");
        AssertContains("KimiSkillsDirsList", xaml, "profile editor should expose Kimi skills directories");
        AssertContains("KimiMcpConfigBox", xaml, "profile editor should expose Kimi MCP config file");
        AssertContains("KimiAdditionalDirsList", xaml, "profile editor should expose Kimi additional workspace directories");
        AssertContains("AddKimiSkillsDir_Click", source, "profile editor should add Kimi skills directories");
        AssertContains("RemoveKimiSkillsDir_Click", source, "profile editor should remove Kimi skills directories");
        AssertContains("BrowseKimiMcpConfig_Click", source, "profile editor should browse for a Kimi MCP config file");
        AssertContains("AddKimiAdditionalDir_Click", source, "profile editor should add Kimi additional directories");
        AssertContains("RemoveKimiAdditionalDir_Click", source, "profile editor should remove Kimi additional directories");
    }

    private static void PersonaEditorWindow_Source_ShowsKimiMigrationWarning()
    {
        var xaml = ReadRepoFile("Views/PersonaEditorWindow.xaml");
        var source = ReadRepoFile("Views/PersonaEditorWindow.xaml.cs");

        AssertContains("KimiMigrationWarningBorder", xaml, "profile editor should expose a warning banner for legacy Kimi args");
        AssertContains("KimiPersonaMigration.Normalize(Persona)", source, "profile editor should normalize legacy Kimi args when loading");
        AssertContains("UpdateKimiMigrationWarning", source, "profile editor should surface Kimi migration warnings in the UI");
    }

    private static void SettingsWindow_Xaml_ExposesKimiPathSetting()
    {
        var xaml = ReadRepoFile("Views/SettingsWindow.xaml");

        AssertContains("Kimi CLI", xaml, "settings should expose a Kimi CLI path field");
        AssertContains("do not manage Kimi auth or config files", xaml, "settings guidance should clarify that CEM does not own Kimi auth/config");
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

    private static void AssertThrows<TException>(Action action, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new Exception($"{message}. Expected {typeof(TException).Name}, got {ex.GetType().Name}: {ex.Message}");
        }

        throw new Exception($"{message}. Expected {typeof(TException).Name} but no exception was thrown.");
    }

    private static void AssertThrowsContains<TException>(Action action, string expectedMessagePart, string message) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            if (ex.Message.Contains(expectedMessagePart, StringComparison.Ordinal))
                return;

            throw new Exception($"{message}. Expected exception message to contain '{expectedMessagePart}', got '{ex.Message}'.");
        }
        catch (Exception ex)
        {
            throw new Exception($"{message}. Expected {typeof(TException).Name}, got {ex.GetType().Name}: {ex.Message}");
        }

        throw new Exception($"{message}. Expected {typeof(TException).Name} but no exception was thrown.");
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

    private static T GetRequiredPropertyValue<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (property == null)
            throw new Exception($"Expected property '{propertyName}' on type '{instance.GetType().Name}'.");

        var value = property.GetValue(instance);
        if (value is T typed)
            return typed;

        throw new Exception($"Expected property '{propertyName}' on type '{instance.GetType().Name}' to be of type '{typeof(T).Name}', got '{value?.GetType().Name ?? "null"}'.");
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

    private static void Account_BackwardCompatibility_MigratesMissingProviderToCodex()
    {
        var json = "[{\"Id\":\"abc123\",\"Name\":\"Legacy\",\"Type\":\"plus\"}]";
        var accounts = JsonSerializer.Deserialize<List<Account>>(json) ?? new List<Account>();
        var acct = accounts[0];
        AssertEqual<string?>(null, acct.Provider, "legacy JSON should deserialize with null Provider");
        // Simulate AccountManager.GetAccounts migration
        if (string.IsNullOrWhiteSpace(acct.Provider)) acct.Provider = "codex";
        AssertEqual("codex", acct.ResolvedProvider, "legacy account should resolve to codex provider after migration");
    }

    private static void Account_KimiProvider_DerivesIsolatedKimiHome()
    {
        var acct = new Account { Id = "kimi-test-123", Name = "Kimi Test", Provider = "kimi", Type = "kimi_oauth" };
        var home = KimiCliManager.GetKimiCodeHome(acct);
        AssertContains("kimi-accounts", home, "Kimi home should live under kimi-accounts");
        AssertContains(acct.Id, home, "Kimi home should include the account id");
    }

    private static void Account_KimiAndCodexAccounts_HaveDifferentIcons()
    {
        var codexPlus = new Account { Name = "Codex", Provider = "codex", Type = "plus" };
        var codexApi = new Account { Name = "Codex API", Provider = "codex", Type = "api_key" };
        var kimiOauth = new Account { Name = "Kimi", Provider = "kimi", Type = "kimi_oauth" };
        var kimiApi = new Account { Name = "Kimi API", Provider = "kimi", Type = "moonshot_api_key" };

        AssertEqual("🌐", codexPlus.Icon, "Codex Plus icon should be globe");
        AssertEqual("🔑", codexApi.Icon, "Codex API icon should be key");
        AssertEqual("🌙", kimiOauth.Icon, "Kimi OAuth icon should be moon");
        AssertEqual("🔑", kimiApi.Icon, "Kimi API icon should be key");
    }

    private static void LauncherService_ValidateProviderCompatibility_BlocksMismatch()
    {
        var codexAcct = new Account { Name = "Codex", Provider = "codex", Type = "plus" };
        var kimiAcct = new Account { Name = "Kimi", Provider = "kimi", Type = "kimi_oauth" };
        var codexPersona = new Persona { Name = "Planner", ConfigOverrides = new() { ["model"] = "gpt-5.4" } };
        var kimiPersona = new Persona { Name = "Kimi Planner", ConfigOverrides = new() { ["model"] = "kimi-k2.6" } };

        // Use reflection to invoke the private static method
        var method = typeof(LauncherService).GetMethod("ValidateProviderCompatibility", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (method == null) throw new Exception("ValidateProviderCompatibility method not found");

        try
        {
            method.Invoke(null, new object[] { codexAcct, kimiPersona });
            throw new Exception("Codex account + Kimi persona should throw");
        }
        catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException is InvalidOperationException)
        {
            AssertContains("mismatch", tie.InnerException.Message, "should mention mismatch");
        }

        try
        {
            method.Invoke(null, new object[] { kimiAcct, codexPersona });
            throw new Exception("Kimi account + Codex persona should throw");
        }
        catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException is InvalidOperationException)
        {
            AssertContains("mismatch", tie.InnerException.Message, "should mention mismatch");
        }

        // Matching should not throw
        method.Invoke(null, new object[] { codexAcct, codexPersona });
        method.Invoke(null, new object[] { kimiAcct, kimiPersona });
    }

    private static void SessionManager_StaleNullPidSession_WithNoLiveEvidence_IsPruned()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        var config = new ConfigService(baseDir);
        var sm = new SessionManager(config, new FakeBestEffortDesktopTerminator());

        var session = new Session
        {
            Id = "stale-cli-001",
            Type = "cli",
            ProcessId = null,
            KillMarker = "CEM_SESSION_stale-cli-001",
            LauncherScriptPath = Path.Combine(baseDir, "sessions", "stale-cli-001", "launch.cmd"),
            StartedMarkerPath = Path.Combine(baseDir, "sessions", "stale-cli-001", "started.txt"),
            StartTime = DateTime.Now.AddMinutes(-5)
        };

        // Create started marker to prove the session was alive at some point
        Directory.CreateDirectory(Path.GetDirectoryName(session.StartedMarkerPath)!);
        File.WriteAllText(session.StartedMarkerPath, DateTime.Now.ToString("O"));

        sm.Register(session);
        AssertEqual(1, sm.Active.Count, "session should be registered");

        // No live process matching the marker exists, so it should be clearly dead and pruned
        sm.PruneExitedSessions();
        AssertEqual(0, sm.Active.Count, "stale session with no live evidence should be pruned");
    }

    private static void SessionManager_KillSession_AlreadyDeadTrackedPid_RetiresStaleRow()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        var config = new ConfigService(baseDir);
        var sm = new SessionManager(config, new FakeBestEffortDesktopTerminator());

        var session = new Session
        {
            Id = "dead-tracked-001",
            Type = "cli",
            ProcessId = 999999,
            StartTime = DateTime.Now.AddMinutes(-5)
        };

        sm.Register(session);
        var result = sm.KillSession(session.Id);
        AssertEqual(true, result.KillConfirmed, "killing an already-dead tracked session should be confirmed via fallback");
        AssertEqual(true, result.SessionRemoved, "already-dead tracked session should be removed");
    }

    private static void SessionManager_StaleSession_AmbiguousRecentSession_IsRetained()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);
        var config = new ConfigService(baseDir);
        var sm = new SessionManager(config, new FakeBestEffortDesktopTerminator());

        var session = new Session
        {
            Id = "recent-cli-001",
            Type = "cli",
            ProcessId = null,
            KillMarker = "CEM_SESSION_recent-cli-001",
            LauncherScriptPath = Path.Combine(baseDir, "sessions", "recent-cli-001", "launch.cmd"),
            StartTime = DateTime.Now.AddSeconds(-5) // very recent
        };

        sm.Register(session);
        sm.PruneExitedSessions();
        AssertEqual(1, sm.Active.Count, "very recent session with no evidence should be retained as ambiguous");
    }

    private static void KimiCliManager_AccountAwareLogin_SetsKimiCodeHome()
    {
        var source = ReadRepoFile("Services/KimiCliManager.cs");
        AssertContains("KIMI_CODE_HOME", source, "Kimi CLI manager should set KIMI_CODE_HOME");
        AssertContains("GetKimiCodeHome", source, "Kimi CLI manager should derive home per account");
        AssertContains("RunLogin(Account", source, "Kimi CLI manager should support account-scoped login");
    }

    private static void SettingsWindow_Xaml_RemovesGlobalKimiLoginButton()
    {
        var xaml = ReadRepoFile("Views/SettingsWindow.xaml");
        AssertNotContains("Kimi Login / Setup", xaml, "settings should no longer expose a global Kimi login button");
    }

    private static void AccountWizardWindow_Source_SupportsCodexAndKimi()
    {
        var xaml = ReadRepoFile("Views/AccountWizardWindow.xaml");
        var source = ReadRepoFile("Views/AccountWizardWindow.xaml.cs");

        AssertContains("Provider", xaml, "wizard should expose provider selection");
        AssertContains("Authentication", xaml, "wizard should expose auth type selection");
        AssertContains("kimi_oauth", source, "wizard should support Kimi Code OAuth");
        AssertContains("moonshot_api_key", source, "wizard should support Moonshot API Key");
        AssertContains("AddKimiOAuthAccount", source, "wizard should call AddKimiOAuthAccount");
        AssertContains("AddKimiApiKeyAccount", source, "wizard should call AddKimiApiKeyAccount");
    }

    private static void LauncherService_FiltersStaleConfigProfileOverride()
    {
        var method = typeof(LauncherService).GetMethod(
            "FilterNonProfileCliArgs",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
            throw new Exception("FilterNonProfileCliArgs should exist as a private static method.");

        var legacyArgs = new List<string>
        {
            "-c", "profile=\"cem_old_profile\"",
            "--config", "profile=\"cem_old_profile\"",
            "-c=profile=\"cem_old_profile\"",
            "--config=profile=\"cem_old_profile\""
        };

        var result = (IEnumerable<string>)method.Invoke(null, new object[] { legacyArgs })!;
        var filtered = result.ToList();

        AssertEqual(0, filtered.Count, "legacy -c profile=... and --config profile=... overrides should be completely filtered out");
    }

    private static void LauncherService_PreservesNonProfileConfigOverride()
    {
        var method = typeof(LauncherService).GetMethod(
            "FilterNonProfileCliArgs",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
            throw new Exception("FilterNonProfileCliArgs should exist as a private static method.");

        var args = new List<string>
        {
            "--color=always",
            "-c", "some_safe_key=value",
            "--config", "another_safe_key=value"
        };

        var result = (IEnumerable<string>)method.Invoke(null, new object[] { args })!;
        var filtered = result.ToList();

        var filteredText = string.Join(" ", filtered);
        AssertEqual(5, filtered.Count, "non-profile-controlled config overrides should be preserved");
        AssertContains("--color=always", filteredText, "--color=always should be preserved");
        AssertContains("-c", filteredText, "-c flag for safe key should be preserved");
        AssertContains("some_safe_key=value", filteredText, "safe config override value should be preserved");
        AssertContains("--config", filteredText, "--config flag for safe key should be preserved");
        AssertContains("another_safe_key=value", filteredText, "safe config override value should be preserved");
    }

    private static void ConfigService_StripsStalePersistedConfigProfileOverride()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);

        try
        {
            var config = new ConfigService(baseDir);
            var personas = new List<Persona>
            {
                new Persona
                {
                    Name = "Test Profile",
                    CliArgs = new List<string>
                    {
                        "-c", "profile=\"cem_old\"",
                        "--config", "profile=\"cem_old\"",
                        "--model", "bad-model"
                    }
                }
            };
            config.SaveList("personas", personas);
            config.EnsureDefaults();

            var result = config.LoadList<Persona>("personas");
            var testPersona = result.FirstOrDefault(p => string.Equals(p.Name, "Test Profile", StringComparison.OrdinalIgnoreCase));
            if (testPersona == null)
                throw new Exception("Test persona should survive EnsureDefaults");

            AssertEqual(0, testPersona.CliArgs.Count, "stale -c profile=..., --config profile=..., and --model should all be stripped by migration");
        }
        finally
        {
            try { if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true); } catch { }
        }
    }

    private static void ConfigService_PreservesSafePersistedConfigOverride()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "cem-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(baseDir);

        try
        {
            var config = new ConfigService(baseDir);
            var personas = new List<Persona>
            {
                new Persona
                {
                    Name = "Safe Args Profile",
                    CliArgs = new List<string>
                    {
                        "--color=always",
                        "-c", "some_safe_key=value"
                    }
                }
            };
            config.SaveList("personas", personas);
            config.EnsureDefaults();

            var result = config.LoadList<Persona>("personas");
            var testPersona = result.FirstOrDefault(p => string.Equals(p.Name, "Safe Args Profile", StringComparison.OrdinalIgnoreCase));
            if (testPersona == null)
                throw new Exception("Test persona should survive EnsureDefaults");

            AssertEqual(3, testPersona.CliArgs.Count, "safe non-profile CLI args should be preserved by migration");
            AssertEqual("--color=always", testPersona.CliArgs[0], "--color=always should be preserved");
            AssertEqual("-c", testPersona.CliArgs[1], "-c flag for safe key should be preserved");
            AssertEqual("some_safe_key=value", testPersona.CliArgs[2], "safe config override value should be preserved");
        }
        finally
        {
            try { if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true); } catch { }
        }
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
