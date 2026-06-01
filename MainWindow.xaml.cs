using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using CodexEnvironmentManager.Models;
using CodexEnvironmentManager.Services;
using CodexEnvironmentManager.Views;
using WpfMessageBox = System.Windows.MessageBox;
using WinOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace CodexEnvironmentManager;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ConfigService _config;
    private readonly AccountManager _accountManager;
    private readonly PersonaEngine _personaEngine;
    private readonly WorkspaceManager _workspaceManager;
    private readonly SessionManager _sessionManager;
    private readonly LauncherService _launcher;
    private readonly CodexProcessManager _processManager;
    private readonly KimiCliManager _kimiCliManager;
    private readonly DesktopWorkspaceLauncher _desktopWorkspaceLauncher;
    private readonly GitStateGuard _gitGuard;
    private readonly TrayService _tray;
    private readonly LogService _log;
    private DispatcherTimer _healthTimer = null!;
    private bool _isExplicitExit;
    private bool _firstRunChecked;

    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<Persona> Personas { get; } = new();
    public ObservableCollection<Workspace> Workspaces { get; } = new();
    public ObservableCollection<SessionViewModel> ActiveSessions { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _log = new LogService();
        _config = new ConfigService();
        _config.EnsureDefaults();
        _accountManager = new AccountManager(_config, _log);
        _personaEngine = new PersonaEngine();
        _workspaceManager = new WorkspaceManager(_config);
        _processManager = new CodexProcessManager(_log);
        _sessionManager = new SessionManager(_config, _processManager);
        _kimiCliManager = new KimiCliManager(_config, _log);
        _desktopWorkspaceLauncher = new DesktopWorkspaceLauncher(_processManager, _log);
        _gitGuard = new GitStateGuard();
        _launcher = new LauncherService(_accountManager, _personaEngine, _sessionManager, _processManager, _desktopWorkspaceLauncher, _gitGuard, _kimiCliManager, _log, _config);
        _tray = new TrayService(this);

        ApplyCodexDesktopOverride();

        LoadData();
        RefreshSessions();
        StartHealthTimer();
        Loaded += MainWindow_Loaded;
        _log.Info("MainWindow initialized");
    }

    public void DisposeTray() => _tray.Dispose();

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_firstRunChecked) return;
        _firstRunChecked = true;

        try
        {
            var accounts = _accountManager.GetAccounts();
            var settings = _config.LoadList<AppSettings>("settings");
            if (accounts.Count == 0 || settings.Count == 0 || !settings.Any(s => s.OnboardingCompleted))
            {
                _log.Info("First-run wizard triggered");
                var wizard = new FirstRunWizard(_config, _accountManager, _log) { Owner = this };
                wizard.ShowDialog();
                ApplyCodexDesktopOverride();
                LoadData();
                RefreshSessions();
            }
        }
        catch (Exception ex)
        {
            _log.Error("First-run wizard failed", ex);
            WpfMessageBox.Show($"First-run setup failed:{Environment.NewLine}{ex.Message}", "First-run Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyCodexDesktopOverride()
    {
        try
        {
            var setting = _config.LoadList<AppSettings>("settings").FirstOrDefault();
            _processManager.OverridePath = CodexProcessManager.NormalizeDesktopOverridePath(setting?.CodexDesktopPath);
        }
        catch (Exception ex)
        {
            _log.Error("Failed to apply Codex Desktop override", ex);
        }
    }


    void LoadData()
    {
        try
        {
            Accounts.Clear();
            foreach (var a in _accountManager.GetAccounts()) Accounts.Add(a);
            AccountList.ItemsSource = Accounts;
            AccountCombo.ItemsSource = Accounts;
            RefreshActiveAccount();
            UpdateAccountActionAvailability();

            Personas.Clear();
            foreach (var p in _config.LoadList<Persona>("personas")) Personas.Add(p);
            PersonaList.ItemsSource = Personas;
            PersonaCombo.ItemsSource = Personas;

            Workspaces.Clear();
            foreach (var w in _workspaceManager.GetWorkspaces()) Workspaces.Add(w);
            WorkspaceList.ItemsSource = Workspaces;
            WorkspaceCombo.ItemsSource = Workspaces;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("corrupted"))
        {
            _log.Error("Config corruption detected", ex);
            WpfMessageBox.Show($"Configuration file corrupted:\n\n{ex.Message}\n\nThe file has been backed up. Please restart the app.", "Config Error", MessageBoxButton.OK, MessageBoxImage.Error);
            System.Windows.Application.Current.Shutdown();
        }
    }

    void RefreshActiveAccount()
    {
        string? activeId = null;
        try { activeId = JunctionManager.LoadActiveAccount(); }
        catch { /* corrupted active_account.json — ignore */ }

        string activeName = "(none)";
        foreach (var a in Accounts) a.IsActive = false;

        if (!string.IsNullOrEmpty(activeId))
        {
            var active = Accounts.FirstOrDefault(a => a.Id == activeId);
            if (active != null)
            {
                active.IsActive = true;
                activeName = active.Display;
            }
            else
            {
                activeName = $"(unknown account: {activeId})";
            }
        }
        else if (Directory.Exists(JunctionManager.CodexHome))
        {
            try
            {
                var attr = File.GetAttributes(JunctionManager.CodexHome);
                if ((attr & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint)
                    activeName = "(legacy folder — not managed)";
            }
            catch { /* can't read attributes */ }
        }

        ActiveAccountText.Text = activeName;
        AccountList.Items.Refresh();
    }

    void RefreshSessions()
    {
        _sessionManager.PruneExitedSessions();

        ActiveSessions.Clear();
        foreach (var s in _sessionManager.Active)
        {
            s.AccountName = Accounts.FirstOrDefault(a => a.Id == s.AccountId)?.Name ?? "?";
            s.PersonaName = Personas.FirstOrDefault(p => p.Id == s.PersonaId)?.Name ?? "?";
            s.WorkspaceName = Workspaces.FirstOrDefault(w => w.Id == s.WorkspaceId)?.Name ?? "?";
            var vm = new SessionViewModel(s);
            var inspection = _sessionManager.InspectSession(s);
            if (inspection.State == SessionLiveState.Ambiguous && !string.IsNullOrWhiteSpace(inspection.Message))
            {
                vm.StatusText = inspection.Message;
            }
            else if (inspection.State == SessionLiveState.ClearlyDead)
            {
                vm.StatusText = "(exited)";
            }
            else
            {
                vm.StatusText = s.Type switch
                {
                    "cli" => "(cli)",
                    "kimi-cli" => "(kimi)",
                    "desktop_store" => string.IsNullOrWhiteSpace(s.ProfileVerificationStatus) ? "(desktop, profile unverified)" : $"(desktop, {s.ProfileVerificationStatus})",
                    _ => string.IsNullOrWhiteSpace(s.ProfileVerificationStatus) ? "(desktop, profile unverified)" : $"(desktop, {s.ProfileVerificationStatus})"
                };
            }
            ActiveSessions.Add(vm);
        }
        SessionItems.ItemsSource = ActiveSessions;
    }

    void StartHealthTimer()
    {
        _healthTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _healthTimer.Tick += (s, e) =>
        {
            RefreshActiveAccount();
            RefreshSessions();
        };
        _healthTimer.Start();
    }

    // --- Selection sync + pre-fill ---
    private void AccountList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AccountList.SelectedItem is Account a) AccountCombo.SelectedItem = a;
        UpdateAccountActionAvailability();
    }

    private void AccountCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AccountCombo.SelectedItem is Account a) AccountList.SelectedItem = a;
        UpdateAccountActionAvailability();
    }

    private void PersonaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PersonaList.SelectedItem is Persona p) PersonaCombo.SelectedItem = p;
    }
    private void WorkspaceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WorkspaceList.SelectedItem is not Workspace ws) return;
        WorkspaceCombo.SelectedItem = ws;
        if (!string.IsNullOrEmpty(ws.LastAccountId))
        {
            var lastAcct = Accounts.FirstOrDefault(a => a.Id == ws.LastAccountId);
            if (lastAcct != null) AccountCombo.SelectedItem = lastAcct;
        }
        if (!string.IsNullOrEmpty(ws.LastPersonaId))
        {
            var lastPers = Personas.FirstOrDefault(p => p.Id == ws.LastPersonaId);
            if (lastPers != null) PersonaCombo.SelectedItem = lastPers;
        }
        UpdateAccountActionAvailability();
    }

    private void UpdateAccountActionAvailability()
    {
        var account = GetSelectedAccountContext();
        var isKimi = account != null && ProviderCapabilities.IsKimiProvider(account.ResolvedProvider);
        KimiSetupButton.Visibility = isKimi ? Visibility.Visible : Visibility.Collapsed;
    }

    private Account? GetSelectedAccountContext() =>
        AccountCombo.SelectedItem as Account ?? AccountList.SelectedItem as Account;

    // --- Account CRUD ---
    private void AddAccount_Click(object sender, RoutedEventArgs e)
    {
        var wizard = new AccountWizardWindow(_accountManager) { Owner = this };
        if (wizard.ShowDialog() == true)
        {
            LoadData();
        }
    }

    private void EditAccount_Click(object sender, RoutedEventArgs e)
    {
        if (AccountList.SelectedItem is not Account a) return;
        var dlg = new InputDialog("Rename account:", a.Name);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.ResponseText))
        {
            var list = _accountManager.GetAccounts();
            var acct = list.FirstOrDefault(x => x.Id == a.Id);
            if (acct != null)
            {
                var newName = dlg.ResponseText.Trim();
                if (list.Any(x => x.Id != a.Id && string.Equals(x.Name, newName, StringComparison.OrdinalIgnoreCase)))
                {
                    WpfMessageBox.Show($"An account named '{newName}' already exists.", "Duplicate Account", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                acct.Name = newName;
                _config.SaveList("accounts", list);
                LoadData();
            }
        }
    }

    private void KimiSetup_Click(object sender, RoutedEventArgs e)
    {
        var acct = GetSelectedAccountContext();
        if (acct is null)
        {
            WpfMessageBox.Show("Select a Kimi account first.", "Kimi Setup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!ProviderCapabilities.IsKimiProvider(acct.ResolvedProvider))
        {
            WpfMessageBox.Show("Kimi setup is only available for Kimi accounts.", "Kimi Setup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            StatusText.Text = $"Launching Kimi setup for {acct.Name}...";
            _kimiCliManager.RunLogin(acct);
            StatusText.Text = $"Kimi setup opened for {acct.Name}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            _log.Error("Kimi setup failed", ex);
            WpfMessageBox.Show($"Kimi setup failed:\n{ex.Message}", "Kimi Setup", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteAccount_Click(object sender, RoutedEventArgs e)
    {
        if (AccountList.SelectedItem is Account a)
        {
            if (!_accountManager.CanDelete(a.Id))
            {
                WpfMessageBox.Show($"Cannot delete '{a.Name}' because it is the currently active account. Switch to another account first.", "Active Account", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_sessionManager.Active.Any(s => s.AccountId == a.Id))
            {
                WpfMessageBox.Show($"Cannot delete '{a.Name}' while it has active sessions. Kill them first.", "Active Sessions", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (WpfMessageBox.Show($"Delete account '{a.Name}'? This removes its profile data.", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    var result = _accountManager.DeleteAccount(a.Id);
                    LoadData();

                    if (!result.DeletedFromDisk)
                    {
                        WpfMessageBox.Show(result.Message, "Account Removed - Cleanup Needed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        StatusText.Text = result.Message;
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("Account deletion failed", ex);
                    WpfMessageBox.Show(
                        $"Account deletion failed safely. No UI crash.\n\n{ex.Message}",
                        "Delete Account Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    LoadData();
                }
            }
        }
    }

    // --- Profile CRUD ---
    private void AddPersona_Click(object sender, RoutedEventArgs e)
    {
        var editor = new PersonaEditorWindow(new Persona
        {
            Name = "Custom",
            Icon = "\U0001F464",
            AgentsTemplatePath = "Templates/personas/custom_profile.md",
            ApprovalsReviewer = "user",
            ConfigOverrides = new()
            {
                ["model"] = "gpt-5.4",
                ["model_reasoning_effort"] = "high",
                ["approval_policy"] = "on-request",
                ["sandbox_mode"] = "read-only"
            },
            CliArgs = new()
        })
        {
            Owner = this
        };

        if (editor.ShowDialog() == true)
        {
            var list = _config.LoadList<Persona>("personas");
            var name = editor.Persona.Name.Trim();
            if (list.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                WpfMessageBox.Show($"A profile named '{name}' already exists.", "Duplicate Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            EnsurePersonaTemplateExists(editor.Persona);
            list.Add(editor.Persona);
            _config.SaveList("personas", list);
            LoadData();
            if (editor.RefreshInstructionsRequested)
                RefreshInstructionsForAllAccounts(showResult: true);
            else
                PromptRefreshInstructions();
        }
    }

    private void EditPersona_Click(object sender, RoutedEventArgs e)
    {
        if (PersonaList.SelectedItem is not Persona p) return;
        var list = _config.LoadList<Persona>("personas");
        var original = list.FirstOrDefault(x => x.Id == p.Id);
        if (original == null) return;

        var editor = new PersonaEditorWindow(original) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            var edited = editor.Persona;
            if (list.Any(x => x.Id != p.Id && string.Equals(x.Name, edited.Name, StringComparison.OrdinalIgnoreCase)))
            {
                WpfMessageBox.Show($"A profile named '{edited.Name}' already exists.", "Duplicate Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            EnsurePersonaTemplateExists(edited);
            var index = list.FindIndex(x => x.Id == p.Id);
            if (index >= 0) list[index] = edited;
            _config.SaveList("personas", list);
            LoadData();
            if (editor.RefreshInstructionsRequested)
                RefreshInstructionsForAllAccounts(showResult: true);
            else
                PromptRefreshInstructions();
        }
    }

    private void DeletePersona_Click(object sender, RoutedEventArgs e)
    {
        if (PersonaList.SelectedItem is Persona p)
        {
            if (Workspaces.Any(w => w.LastPersonaId == p.Id) || _sessionManager.Active.Any(s => s.PersonaId == p.Id))
            {
                WpfMessageBox.Show($"Cannot delete '{p.Name}' because it is referenced by a previous or active workspace session.", "Profile In Use", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (WpfMessageBox.Show($"Delete profile '{p.Name}'?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            var list = _config.LoadList<Persona>("personas");
            list.RemoveAll(x => x.Id == p.Id);
            _config.SaveList("personas", list);
            LoadData();
            PromptRefreshInstructions();
        }
    }

    // --- Workspace CRUD ---
    private void AddWorkspace_Click(object sender, RoutedEventArgs e)
    {
        var nameDlg = new InputDialog("Enter workspace name:", "MyProject");
        nameDlg.Owner = this;
        if (nameDlg.ShowDialog() != true) return;
        var fbd = new System.Windows.Forms.FolderBrowserDialog { Description = "Select project folder" };
        if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            if (!Directory.Exists(fbd.SelectedPath))
            {
                WpfMessageBox.Show("Selected path does not exist.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                _workspaceManager.AddWorkspace(nameDlg.ResponseText.Trim(), fbd.SelectedPath);
                LoadData();
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(ex.Message, "Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void EditWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (WorkspaceList.SelectedItem is not Workspace w) return;
        var dlg = new InputDialog("Rename workspace:", w.Name);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.ResponseText))
        {
            var list = _workspaceManager.GetWorkspaces();
            var ws = list.FirstOrDefault(x => x.Id == w.Id);
            if (ws != null)
            {
                var newName = dlg.ResponseText.Trim();
                if (list.Any(x => x.Id != w.Id && string.Equals(x.Name, newName, StringComparison.OrdinalIgnoreCase)))
                {
                    WpfMessageBox.Show($"A workspace named '{newName}' already exists.", "Duplicate Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                ws.Name = newName;
                _config.SaveList("workspaces", list);
                LoadData();
            }
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (WorkspaceList.SelectedItem is not Workspace w) return;
        if (Directory.Exists(w.Path))
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = w.Path, UseShellExecute = true });
        else
            WpfMessageBox.Show("Workspace folder no longer exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void DeleteWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (WorkspaceList.SelectedItem is Workspace w)
        {
            var active = _sessionManager.Active.Any(s => s.WorkspaceId == w.Id);
            if (active)
            {
                WpfMessageBox.Show($"Cannot delete '{w.Name}' while it has active sessions. Kill them first.", "Active Sessions", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (WpfMessageBox.Show($"Delete workspace '{w.Name}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _workspaceManager.DeleteWorkspace(w.Id);
                LoadData();
            }
        }
    }

    // --- Launch ---
    private bool ConfirmLaunch(Account acct, Persona persona, Workspace ws, string launchType)
    {
        try
        {
            var preview = _launcher.BuildLaunchPreview(acct, persona, ws, launchType);
            return WpfMessageBox.Show(
                preview + Environment.NewLine + "Proceed with this launch?",
                "Verify Launch Contract",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information) == MessageBoxResult.Yes;
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Launch validation failed:\n{ex.Message}", "Launch Validation", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool ConfirmKimiLaunch(Account acct, Persona? persona, Workspace ws)
    {
        try
        {
            var preview = _launcher.BuildKimiLaunchPreview(acct, persona, ws);
            return WpfMessageBox.Show(
                preview + Environment.NewLine + "Proceed with this launch?",
                "Verify Kimi Launch Contract",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information) == MessageBoxResult.Yes;
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Kimi launch validation failed:\n{ex.Message}", "Launch Validation", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }


    private void ViewGeneratedFiles_Click(object sender, RoutedEventArgs e)
    {
        if (PersonaCombo.SelectedItem is Persona selectedPersona && selectedPersona.IsKimiProvider)
        {
            WpfMessageBox.Show("Kimi profiles do not generate Codex account files.", "View Generated Files", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (AccountCombo.SelectedItem is not Account acct || PersonaCombo.SelectedItem is not Persona persona)
        {
            WpfMessageBox.Show("Select an account and profile first.", "View Generated Files", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var accountHome = JunctionManager.GetAccountProfilePath(acct.Id);
            var profileName = PersonaEngine.GetProfileName(persona);
            var configPath = Path.Combine(accountHome, "config.toml");
            var agentsPath = Path.Combine(accountHome, "AGENTS.md");
            var instructionsPath = Path.Combine(accountHome, "cem-profiles", profileName + ".instructions.md");
            var profileConfigPath = Path.Combine(accountHome, profileName + ".config.toml");

            var viewer = new GeneratedFilesViewerWindow(
                acct.Name,
                persona.Name,
                accountHome,
                configPath,
                agentsPath,
                instructionsPath,
                profileConfigPath)
            {
                Owner = this
            };
            viewer.ShowDialog();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Failed to open generated file viewer:{Environment.NewLine}{ex.Message}", "View Generated Files", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LaunchDesktop_Click(object sender, RoutedEventArgs e)
    {
        if (AccountCombo.SelectedItem is not Account acct || PersonaCombo.SelectedItem is not Persona persona || WorkspaceCombo.SelectedItem is not Workspace ws)
        {
            WpfMessageBox.Show("Select an account, profile, and workspace.");
            return;
        }
        if (!ProviderCapabilities.ForProvider(acct.ResolvedProvider).SupportsDesktop)
        {
            StatusText.Text = "Desktop App not available for Kimi";
            WpfMessageBox.Show("Desktop App not available for Kimi", "Desktop Launch", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!ConfirmLaunch(acct, persona, ws, "desktop")) return;
        try
        {
            StatusText.Text = "Launching Desktop...";
            var desktopLaunchStatus = _launcher.LaunchDesktop(acct, persona, ws);
            _workspaceManager.UpdateLastSession(ws.Id, acct.Id, persona.Id);
            var wlist = _workspaceManager.GetWorkspaces();
            var w = wlist.FirstOrDefault(x => x.Id == ws.Id);
            if (w != null) { w.LastLaunchType = "desktop"; _config.SaveList("workspaces", wlist); }
            RefreshSessions();
            RefreshActiveAccount();
            StatusText.Text = desktopLaunchStatus;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            _log.Error("Desktop launch failed", ex);
            WpfMessageBox.Show($"Launch failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LaunchCli_Click(object sender, RoutedEventArgs e)
    {
        if (AccountCombo.SelectedItem is not Account acct || PersonaCombo.SelectedItem is not Persona persona || WorkspaceCombo.SelectedItem is not Workspace ws)
        {
            WpfMessageBox.Show("Select an account, profile, and workspace.");
            return;
        }
        var accountCapabilities = ProviderCapabilities.ForProvider(acct.ResolvedProvider);
        var isKimi = ProviderCapabilities.IsKimiProvider(acct.ResolvedProvider);
        if (!accountCapabilities.SupportsCli)
        {
            WpfMessageBox.Show($"{acct.ResolvedProvider} does not support CLI launches.", "Launch", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (isKimi)
        {
            if (!ConfirmKimiLaunch(acct, persona, ws)) return;
        }
        else if (!ConfirmLaunch(acct, persona, ws, "cli"))
        {
            return;
        }

        try
        {
            StatusText.Text = isKimi ? "Launching Kimi CLI..." : "Launching CLI Companion...";
            if (isKimi)
                _launcher.LaunchKimiCompanion(acct, persona, ws);
            else
                _launcher.LaunchCliCompanion(acct, persona, ws);
            _workspaceManager.UpdateLastSession(ws.Id, acct.Id, persona.Id);
            var wlist = _workspaceManager.GetWorkspaces();
            var w = wlist.FirstOrDefault(x => x.Id == ws.Id);
            if (w != null) { w.LastLaunchType = "cli"; _config.SaveList("workspaces", wlist); }
            RefreshSessions();
            Dispatcher.BeginInvoke(new Action(RefreshSessions), DispatcherPriority.Background);
            StatusText.Text = isKimi ? "Kimi CLI launched." : "CLI Companion launched.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            _log.Error(isKimi ? "Kimi launch failed" : "CLI launch failed", ex);
            WpfMessageBox.Show($"Launch failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResumeLast_Click(object sender, RoutedEventArgs e)
    {
        if (WorkspaceCombo.SelectedItem is not Workspace ws)
        {
            WpfMessageBox.Show("Select a workspace first.");
            return;
        }
        if (string.IsNullOrEmpty(ws.LastAccountId) || string.IsNullOrEmpty(ws.LastPersonaId))
        {
            WpfMessageBox.Show("No previous session found for this workspace. Launch manually once to establish history.");
            return;
        }
        var acct = Accounts.FirstOrDefault(a => a.Id == ws.LastAccountId);
        var persona = Personas.FirstOrDefault(p => p.Id == ws.LastPersonaId);
        if (acct == null || persona == null)
        {
            WpfMessageBox.Show("Previous account or profile no longer exists.");
            return;
        }
        AccountCombo.SelectedItem = acct;
        PersonaCombo.SelectedItem = persona;
        if (string.Equals(acct.ResolvedProvider, "kimi", StringComparison.OrdinalIgnoreCase) && ws.LastLaunchType != "cli")
        {
            // Kimi has no desktop launch; force CLI resume.
            LaunchCli_Click(sender, e);
            return;
        }
        if (ws.LastLaunchType == "cli")
            LaunchCli_Click(sender, e);
        else
            LaunchDesktop_Click(sender, e);
    }

    // --- Session controls ---
    private void KillSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button b && b.Tag is string sessionId)
        {
            var result = _sessionManager.KillSession(sessionId);
            if (!result.KillConfirmed)
            {
                WpfMessageBox.Show(result.Message, "Kill Session", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            RefreshSessions();
        }
    }

    private void SnapshotSession_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button b && b.Tag is string sessionId)
        {
            var s = _sessionManager.Active.FirstOrDefault(x => x.Id == sessionId);
            if (s == null) return;
            if (string.Equals(s.Type, "kimi-cli", StringComparison.OrdinalIgnoreCase))
            {
                WpfMessageBox.Show("Snapshots are not available for Kimi sessions.", "Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var accountPath = JunctionManager.GetAccountProfilePath(s.AccountId);
            var orbitPath = Path.Combine(accountPath, "orbit.db");
            if (File.Exists(orbitPath))
            {
                var snapDir = Path.Combine(JunctionManager.SwitcherDir, "snapshots");
                Directory.CreateDirectory(snapDir);
                var mdPath = Path.Combine(snapDir, $"{s.AccountId}_{DateTime.Now:yyyyMMdd_HHmmss}.md");
                try
                {
                    SnapshotExporter.ExportAccountSnapshot(accountPath, mdPath);
                    WpfMessageBox.Show($"Snapshot exported to:\n{mdPath}", "Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    _log.Error("Snapshot export failed", ex);
                    WpfMessageBox.Show($"Export failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                var historyPath = Path.Combine(accountPath, "history.jsonl");
                if (File.Exists(historyPath))
                {
                    var snapDir = Path.Combine(JunctionManager.SwitcherDir, "snapshots");
                    Directory.CreateDirectory(snapDir);
                    var mdPath = Path.Combine(snapDir, $"{s.AccountId}_{DateTime.Now:yyyyMMdd_HHmmss}.md");
                    try
                    {
                        SnapshotExporter.ExportAccountSnapshot(accountPath, mdPath);
                        WpfMessageBox.Show($"Snapshot exported to:\n{mdPath}", "Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Snapshot export failed", ex);
                        WpfMessageBox.Show($"Export failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    WpfMessageBox.Show("No local orbit.db or history.jsonl found to snapshot.", "Snapshot", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }

    // --- Misc ---
    private void PersonaTemplates_Click(object sender, RoutedEventArgs e)
    {
        var templatesDir = Path.Combine(_config.TemplatesDir, "personas");
        Directory.CreateDirectory(templatesDir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = templatesDir, UseShellExecute = true });
    }

    private void OpenLinkedTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (PersonaList.SelectedItem is not Persona persona)
        {
            WpfMessageBox.Show("Select a CEM profile first.", "Role Template", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var path = EnsurePersonaTemplateExists(persona);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private void RefreshInstructions_Click(object sender, RoutedEventArgs e)
    {
        RefreshInstructionsForAllAccounts(showResult: true);
    }

    private void PromptRefreshInstructions()
    {
        if (WpfMessageBox.Show(
                "Refresh generated instructions for all accounts now?",
                "Refresh Instructions",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            RefreshInstructionsForAllAccounts(showResult: true);
        }
    }

    private void RefreshInstructionsForAllAccounts(bool showResult)
    {
        try
        {
            var accounts = _accountManager.GetAccounts()
                .Where(a => string.Equals(a.ResolvedProvider, "codex", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var personas = _config.LoadList<Persona>("personas")
                .Where(p => !p.IsKimiProvider)
                .ToList();
            var updated = 0;

            foreach (var account in accounts)
            {
                _personaEngine.RefreshRoleCatalogForAccount(account, personas, _log.Warn);
                updated++;
            }

            if (showResult)
                WpfMessageBox.Show(
                    $"Refreshed generated instructions for {updated} account(s).",
                    "Refresh Instructions",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Failed to refresh generated instructions:{Environment.NewLine}{ex.Message}",
                "Refresh Instructions",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private string EnsurePersonaTemplateExists(Persona persona)
    {
        if (string.IsNullOrWhiteSpace(persona.AgentsTemplatePath))
            persona.AgentsTemplatePath = "Templates/personas/" + MakeSafeFileName(persona.Name) + ".md";

        var relative = persona.AgentsTemplatePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (relative.StartsWith("Templates" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            relative = relative[("Templates" + Path.DirectorySeparatorChar).Length..];

        var path = Path.Combine(_config.TemplatesDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# CEM Role: " + persona.Name);
            sb.AppendLine();
            sb.AppendLine("## Mission");
            sb.AppendLine("Define the purpose of this custom CEM profile.");
            sb.AppendLine();
            sb.AppendLine("## Scope");
            sb.AppendLine("- Describe what this profile is allowed to do.");
            sb.AppendLine("- Describe what this profile must not do.");
            sb.AppendLine();
            sb.AppendLine("## Workflow");
            sb.AppendLine("1. Read the active launch context.");
            sb.AppendLine("2. Follow the selected Codex profile configuration.");
            sb.AppendLine("3. Keep work scoped to the selected workspace.");
            File.WriteAllText(path, sb.ToString());
        }

        return path;
    }

    private static string MakeSafeFileName(string? value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((value ?? "custom_profile")
            .ToLowerInvariant()
            .Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c)
            .ToArray());

        while (cleaned.Contains("__", StringComparison.Ordinal))
            cleaned = cleaned.Replace("__", "_");

        cleaned = cleaned.Trim('_');
        return string.IsNullOrWhiteSpace(cleaned) ? "custom_profile" : cleaned;
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var sw = new SettingsWindow(_config, _log, _kimiCliManager);
        sw.Owner = this;
        if (sw.ShowDialog() == true)
        {
            var settings = _config.LoadList<AppSettings>("settings").FirstOrDefault();
            _processManager.OverridePath = CodexProcessManager.NormalizeDesktopOverridePath(settings?.CodexDesktopPath);
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        try { DragMove(); }
        catch { /* Ignore drag races during window state transitions. */ }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximizeRestore();
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => RequestExit();

    public void RequestExit()
    {
        _isExplicitExit = true;
        _healthTimer?.Stop();
        _tray.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExplicitExit) return;

        var settings = _config.LoadList<AppSettings>("settings").FirstOrDefault();
        if (settings?.MinimizeToTray ?? true)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _healthTimer?.Stop();
            _tray.Dispose();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class SessionViewModel
{
    private readonly Session _session;
    public SessionViewModel(Session s) => _session = s;
    public string Id => _session.Id;
    public string DisplayName => _session.DisplayName;
    public string RequestedProfileName => _session.RequestedProfileName;
    public string RequestedCodexProfileName => _session.RequestedCodexProfileName;
    public string ProfileLaunchMethod => _session.ProfileLaunchMethod;
    public string ProfileVerificationStatus => _session.ProfileVerificationStatus;
    public string ProfileLaunchCommandPreview => _session.ProfileLaunchCommandPreview;
    public string TypeIcon => _session.Type switch
    {
        "kimi-cli" => "Kimi",
        _ when _session.Type.StartsWith("desktop", StringComparison.OrdinalIgnoreCase) => "Desktop",
        _ => "CLI"
    };
    public DateTime StartTime => _session.StartTime;
    public string StatusText { get; set; } = "";
    public bool CanSnapshot => !string.Equals(_session.Type, "kimi-cli", StringComparison.OrdinalIgnoreCase);
}
