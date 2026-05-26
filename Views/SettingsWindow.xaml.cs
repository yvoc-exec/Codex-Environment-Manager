using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using CodexEnvironmentManager.Models;
using CodexEnvironmentManager.Services;
using WinOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace CodexEnvironmentManager.Views;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _config;
    private readonly LogService _log;
    private readonly KimiCliManager _kimiCliManager;
    private AppSettings _settings = new();

    public SettingsWindow(ConfigService config, LogService log, KimiCliManager kimiCliManager)
    {
        InitializeComponent();
        _config = config;
        _log = log;
        _kimiCliManager = kimiCliManager;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var list = _config.LoadList<AppSettings>("settings");
        _settings = list.FirstOrDefault() ?? new AppSettings();
        DesktopPathBox.Text = _settings.CodexDesktopPath;
        KimiPathBox.Text = _settings.KimiCliPath;
        if (string.IsNullOrWhiteSpace(KimiPathBox.Text) && _kimiCliManager.TryResolveKimiCliExecutable(out var detectedKimiPath) && !string.IsNullOrWhiteSpace(detectedKimiPath))
        {
            KimiPathBox.Text = detectedKimiPath;
        }
        LogPathBox.Text = _log.GetLogPath();
        TrayCheck.IsChecked = _settings.MinimizeToTray;
        GitGuardCheck.IsChecked = _settings.GitGuardEnabled;
        WindowsTerminalCheck.IsChecked = _settings.PreferWindowsTerminalForCli;
        TrustWorkspaceCheck.IsChecked = _settings.TrustWorkspaceOnLaunch;
        var sandbox = string.IsNullOrWhiteSpace(_settings.WindowsSandboxMode) ? "elevated" : _settings.WindowsSandboxMode;
        foreach (ComboBoxItem item in WindowsSandboxCombo.Items)
        {
            if (string.Equals(item.Content?.ToString(), sandbox, StringComparison.OrdinalIgnoreCase))
            {
                WindowsSandboxCombo.SelectedItem = item;
                break;
            }
        }
    }

    private void BrowseDesktop_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new WinOpenFileDialog
        {
            Title = "Select Codex Desktop executable",
            Filter = "Executable files (*.exe)|*.exe",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() == true)
        {
            DesktopPathBox.Text = dlg.FileName;
        }
    }

    private void BrowseKimi_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new WinOpenFileDialog
        {
            Title = "Select Kimi CLI executable",
            Filter = "Executable files (*.exe;*.cmd;*.bat)|*.exe;*.cmd;*.bat|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dlg.ShowDialog() == true)
        {
            KimiPathBox.Text = dlg.FileName;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var path = DesktopPathBox.Text;
        if (!string.IsNullOrEmpty(path) && !File.Exists(path))
        {
            StatusText.Text = "Selected file does not exist.";
            return;
        }
        var kimiPath = KimiPathBox.Text;
        if (!string.IsNullOrWhiteSpace(kimiPath) && File.Exists(kimiPath) == false && ContainsDirectorySeparator(kimiPath))
        {
            StatusText.Text = "Selected Kimi CLI file does not exist.";
            return;
        }
        _settings.CodexDesktopPath = path;
        _settings.KimiCliPath = kimiPath;
        _settings.MinimizeToTray = TrayCheck.IsChecked ?? true;
        _settings.GitGuardEnabled = GitGuardCheck.IsChecked ?? true;
        _settings.PreferWindowsTerminalForCli = WindowsTerminalCheck.IsChecked ?? false;
        _settings.TrustWorkspaceOnLaunch = TrustWorkspaceCheck.IsChecked ?? true;
        _settings.WindowsSandboxMode = (WindowsSandboxCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "elevated";
        _settings.WriteAgentsMd = false;
        var list = _config.LoadList<AppSettings>("settings");
        list.Clear();
        list.Add(_settings);
        _config.SaveList("settings", list);
        _log.Info("Settings saved");
        DialogResult = true;
        Close();
    }

    private void RefreshAccountConfigs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var requestedSandbox = (WindowsSandboxCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "elevated";
            var accounts = _config.LoadList<Account>("accounts")
                .Where(a => string.Equals(a.ResolvedProvider, "codex", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var updated = 0;

            foreach (var account in accounts)
            {
                PersonaEngine.EnsureAccountRuntimeConfig(account.Id, "", requestedSandbox, false);
                updated++;
            }

            StatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
            StatusText.Text = $"Refreshed {updated} managed account config(s). Existing elevated/unelevated sandbox choices were preserved.";
        }
        catch (Exception ex)
        {
            StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            StatusText.Text = "Refresh failed: " + ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static bool ContainsDirectorySeparator(string value) =>
        value.IndexOf(Path.DirectorySeparatorChar) >= 0 || value.IndexOf(Path.AltDirectorySeparatorChar) >= 0;
}
