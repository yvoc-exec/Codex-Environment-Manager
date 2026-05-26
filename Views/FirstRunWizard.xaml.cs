using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CodexEnvironmentManager.Models;
using CodexEnvironmentManager.Services;
using WpfMessageBox = System.Windows.MessageBox;
using WinOpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace CodexEnvironmentManager.Views;

public partial class FirstRunWizard : Window
{
    private readonly ConfigService _config;
    private readonly AccountManager _accountManager;
    private readonly LogService _log;
    private string? _detectedDesktopPath;

    public bool Completed { get; private set; }

    public FirstRunWizard(ConfigService config, AccountManager accountManager, LogService log)
    {
        InitializeComponent();
        _config = config;
        _accountManager = accountManager;
        _log = log;
        DetectPaths();
    }

    private void DetectPaths()
    {
        var pm = new CodexProcessManager(_log);
        var desktop = pm.DetectCodexDesktop();

        if (desktop.HasExecutable)
        {
            _detectedDesktopPath = desktop.ExecutablePath;
            DesktopPathText.Text = desktop.DisplayText;
            DesktopPathText.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        else if (desktop.HasStoreApp && desktop.HasCliFallback)
        {
            _detectedDesktopPath = null;
            DesktopPathText.Text = desktop.DisplayText;
            DesktopPathText.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        else if (desktop.HasStoreApp)
        {
            _detectedDesktopPath = null;
            DesktopPathText.Text = desktop.DisplayText;
            DesktopPathText.Foreground = System.Windows.Media.Brushes.Gold;
        }
        else if (desktop.HasCliFallback)
        {
            _detectedDesktopPath = null;
            DesktopPathText.Text = desktop.DisplayText;
            DesktopPathText.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        else
        {
            _detectedDesktopPath = null;
            DesktopPathText.Text = desktop.DisplayText;
            DesktopPathText.Foreground = System.Windows.Media.Brushes.OrangeRed;
        }

        if (desktop.HasCliFallback)
        {
            CliStatusText.Text = $"✅ Installed ({desktop.CliPath})";
            CliStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        else
        {
            CliStatusText.Text = "❌ Not found in PATH";
            CliStatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
        }
    }

    private async void ImportDefault_Click(object sender, RoutedEventArgs e)
    {
        ImportButton.IsEnabled = false;
        SkipButton.IsEnabled = false;
        FinishButton.IsEnabled = false;
        StatusText.Foreground = System.Windows.Media.Brushes.LightBlue;
        StatusText.Text = "Importing existing .codex folder... This may take a moment.";
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);

        try
        {
            var codexHome = JunctionManager.CodexHome;
            if (!Directory.Exists(codexHome))
            {
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                StatusText.Text = "No existing .codex folder found.";
                return;
            }

            var existing = _accountManager.GetAccounts();
            var baseName = "Default";
            var name = baseName;
            var counter = 2;
            while (existing.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
                name = baseName + " " + counter++;

            var acct = new Account { Name = name, Type = "plus", Provider = "codex" };
            var result = await Task.Run(() =>
            {
                JunctionManager.CreateAccountProfile(acct.Id);
                var target = JunctionManager.GetAccountProfilePath(acct.Id);

                var attr = File.GetAttributes(codexHome);
                if ((attr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    _log.Info("First-run: existing .codex is a junction — copying visible contents");

                var stats = CopyDirectory(codexHome, target);
                PersonaEngine.EnsureAccountBaseConfig(acct.Id);
                existing.Add(acct);
                _config.SaveList("accounts", existing);
                return stats;
            });

            StatusText.Foreground = result.Warnings.Count == 0
                ? System.Windows.Media.Brushes.LightGreen
                : System.Windows.Media.Brushes.Gold;
            StatusText.Text = $"Imported '{name}' account. Files copied: {result.FilesCopied}. Warnings: {result.Warnings.Count}.";
            _log.Info($"First-run: imported existing .codex as {name} account. Files copied={result.FilesCopied}; warnings={result.Warnings.Count}");

            var message = result.Warnings.Count == 0
                ? $"Imported '{name}' successfully. Click Finish Setup to continue."
                : $"Imported '{name}' with {result.Warnings.Count} warning(s). Click Finish Setup to continue. See logs for details.";
            WpfMessageBox.Show(this, message, "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            StatusText.Text = $"Import failed: {ex.Message}";
            _log.Error("First-run import failed", ex);
            WpfMessageBox.Show(this, $"Import failed:{Environment.NewLine}{ex.Message}", "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ImportButton.IsEnabled = true;
            SkipButton.IsEnabled = true;
            FinishButton.IsEnabled = true;
        }
    }

    private void SkipImport_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Skipped import. You can add accounts later.";
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
            _detectedDesktopPath = dlg.FileName;
            DesktopPathText.Text = _detectedDesktopPath;
            DesktopPathText.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
    }

    private void InstallCliHelp_Click(object sender, RoutedEventArgs e)
    {
        WpfMessageBox.Show("Install Codex CLI:\n\n  npm i -g @openai/codex\n\nThen restart this app after installation.", "Codex CLI Installation");
    }

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_detectedDesktopPath) && !CodexProcessManager.IsCodexCliInstalled() && _accountManager.GetAccounts().Count == 0)
        {
            var proceed = WpfMessageBox.Show(
                "Setup is incomplete: no Desktop path, no Codex CLI, and no account imported. Continue anyway?",
                "Incomplete Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (proceed != MessageBoxResult.Yes) return;
        }

        SaveCompletedSettings(_detectedDesktopPath);
        Completed = true;
        Close();
    }

    public static AppSettings BuildCompletedSettings(AppSettings? existing, string? desktopPath)
    {
        var settings = existing ?? new AppSettings();
        settings.CodexDesktopPath = desktopPath ?? "";
        settings.OnboardingCompleted = true;
        return settings;
    }

    private void SaveCompletedSettings(string? desktopPath)
    {
        var settings = _config.LoadList<AppSettings>("settings");
        var appSettings = BuildCompletedSettings(settings.FirstOrDefault(), desktopPath);
        settings.Clear();
        settings.Add(appSettings);
        _config.SaveList("settings", settings);
    }

    private sealed class CopyStats
    {
        public int FilesCopied { get; set; }
        public int DirectoriesCopied { get; set; }
        public List<string> Warnings { get; } = new();
    }

    private static CopyStats CopyDirectory(string source, string dest)
    {
        var stats = new CopyStats();
        CopyDirectory(source, dest, stats);
        return stats;
    }

    private static void CopyDirectory(string source, string dest, CopyStats stats)
    {
        Directory.CreateDirectory(dest);
        stats.DirectoriesCopied++;

        foreach (var file in Directory.GetFiles(source))
        {
            var destFile = Path.Combine(dest, Path.GetFileName(file));
            try
            {
                CopyFileAllowingReadWriteSharing(file, destFile);
                stats.FilesCopied++;
            }
            catch (Exception ex)
            {
                stats.Warnings.Add($"Skipped file '{file}': {ex.Message}");
            }
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            try
            {
                var dirAttr = File.GetAttributes(dir);
                if ((dirAttr & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    continue;
                var destSub = Path.Combine(dest, Path.GetFileName(dir));
                CopyDirectory(dir, destSub, stats);
            }
            catch (Exception ex)
            {
                stats.Warnings.Add($"Skipped directory '{dir}': {ex.Message}");
            }
        }
    }

    private static void CopyFileAllowingReadWriteSharing(string sourceFile, string destFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
        using var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var dest = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None);
        source.CopyTo(dest);
    }
}
