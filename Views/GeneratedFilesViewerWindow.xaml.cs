using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfMessageBox = System.Windows.MessageBox;

namespace CodexEnvironmentManager.Views;

public partial class GeneratedFilesViewerWindow : Window
{
    private sealed class ViewableFile
    {
        public string Label { get; }
        public string Path { get; }
        public string Description { get; }

        public ViewableFile(string label, string path, string description)
        {
            Label = label;
            Path = path;
            Description = description;
        }

        public override string ToString() => Label;
    }

    private readonly List<ViewableFile> _files;

    public GeneratedFilesViewerWindow(
        string accountName,
        string profileName,
        string accountHome,
        string configPath,
        string agentsPath,
        string instructionsPath,
        string profileConfigPath)
    {
        InitializeComponent();

        ContextText.Text =
            $"Account: {accountName}   |   Profile: {profileName}" + Environment.NewLine +
            $"CODEX_HOME: {accountHome}" + Environment.NewLine +
            "Read-only viewer. Use Profile Settings / Refresh Instructions to regenerate these files.";

        _files = new List<ViewableFile>
        {
            new("config.toml", configPath, "Codex base configuration. Does not contain legacy profile selectors or tables."),
            new($"{profileName}.config.toml", profileConfigPath, "Per-profile Codex configuration with top-level keys."),
            new("AGENTS.md", agentsPath, "Compact shared CEM account guidance only; no duplicated role catalog."),
            new("profile instructions.md", instructionsPath, "Generated active profile instruction file from the selected user-editable role template.")
        };

        FileCombo.ItemsSource = _files;
        FileCombo.SelectedIndex = 0;
    }

    private void FileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadSelectedFile();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadSelectedFile();
    }

    private void LoadSelectedFile()
    {
        if (FileCombo.SelectedItem is not ViewableFile file)
        {
            ContentBox.Text = "";
            return;
        }

        Title = $"Generated Files Viewer - {file.Label}";
        FileCombo.ToolTip = $"{file.Description}{Environment.NewLine}{file.Path}";

        if (!File.Exists(file.Path))
        {
            ContentBox.Text =
                $"File not found:{Environment.NewLine}{file.Path}{Environment.NewLine}{Environment.NewLine}" +
                "Launch once or use Refresh Instructions to generate this file.";
            return;
        }

        try
        {
            ContentBox.Text = File.ReadAllText(file.Path);
        }
        catch (Exception ex)
        {
            ContentBox.Text = $"Failed to read file:{Environment.NewLine}{file.Path}{Environment.NewLine}{Environment.NewLine}{ex.Message}";
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (FileCombo.SelectedItem is ViewableFile file)
            System.Windows.Clipboard.SetText(file.Path);
    }

    private void CopyContent_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(ContentBox.Text))
            System.Windows.Clipboard.SetText(ContentBox.Text);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
