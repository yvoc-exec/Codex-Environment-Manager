using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using CodexEnvironmentManager.Models;
using WpfMessageBox = System.Windows.MessageBox;

namespace CodexEnvironmentManager.Views;

public partial class PersonaEditorWindow : Window
{
    private sealed class Choice
    {
        public string Value { get; }
        public string Label { get; }
        public string Description { get; }

        public Choice(string value, string label, string description)
        {
            Value = value;
            Label = label;
            Description = description;
        }

        public override string ToString() => Label;
    }

    public Persona Persona { get; private set; }
    public bool RefreshInstructionsRequested { get; private set; }

    private readonly string _templatesDir;
    private readonly List<string> _cliArgs = new();
    private readonly Dictionary<string, string> _envVars = new(StringComparer.OrdinalIgnoreCase);
    private string _selectedTemplatePath = string.Empty;

    private static readonly Choice[] IconChoices =
    {
        new("👤", "👤 Default", "Generic profile icon."),
        new("🧠", "🧠 Planner", "Good fit for planning and review oriented profiles."),
        new("🔎", "🔎 Reviewer", "Good fit for review and audit oriented profiles."),
        new("🛠️", "🛠️ Implementor", "Good fit for implementation oriented profiles."),
        new("⚙️", "⚙️ Utility", "General utility or infrastructure profile."),
        new("🚀", "🚀 High-power", "High initiative or advanced profile."),
        new("📝", "📝 Writer", "Documentation or note-taking oriented profile."),
    };

    private static readonly Choice[] ModelChoices =
    {
        new("gpt-5.4", "gpt-5.4", "Balanced coding model."),
        new("gpt-5.4-mini", "gpt-5.4-mini", "Lower-cost, faster model for lighter execution tasks."),
        new("gpt-5.5", "gpt-5.5", "Strongest reasoning model currently used in this workflow."),
    };

    private static readonly Choice[] ReasoningChoices =
    {
        new("minimal", "minimal", "Fastest; least reasoning."),
        new("low", "low", "Faster and cheaper; less deep analysis."),
        new("medium", "medium", "Balanced reasoning."),
        new("high", "high", "Deeper reasoning; better for planning and review."),
        new("xhigh", "extra-high", "Most thorough reasoning; slower and heavier."),
    };

    private static readonly Choice[] SandboxChoices =
    {
        new("read-only", "read-only", "Codex can inspect files but cannot write to the workspace."),
        new("workspace-write", "workspace-write", "Codex can read and edit files inside the selected workspace."),
        new("danger-full-access", "danger-full-access", "No sandbox protection. Codex may access and modify files outside the workspace. Use only when you fully trust the task."),
    };

    private static readonly Choice[] ApprovalChoices =
    {
        new("untrusted", "untrusted", "Strict mode. Codex asks before many commands or actions."),
        new("on-request", "on-request", "Codex can ask for approval when it needs extra permission."),
        new("never", "never", "Codex will not ask for approval. Blocked actions fail instead of prompting."),
        new("on-failure", "on-failure", "Deprecated. Prefer on-request for interactive use or never for non-interactive use."),
    };

    private static readonly Choice[] ApprovalsReviewerChoices =
    {
        new("user", "user", "Approval requests are shown to you."),
        new("auto_review", "auto_review", "Eligible approval requests are routed through Codex auto-review instead of directly asking you."),
    };

    private static readonly Choice[] CliArgChoices =
    {
        new("--color=auto", "--color=auto", "Automatic ANSI color behavior."),
        new("--color=always", "--color=always", "Always emit ANSI colors."),
        new("--color=never", "--color=never", "Disable ANSI colors."),
    };

    private static readonly Choice[] EnvVarChoices =
    {
        new("CODEX_MODE=plan_review", "CODEX_MODE=plan_review", "Marks the profile as plan/review oriented."),
        new("CODEX_MODE=review_only", "CODEX_MODE=review_only", "Marks the profile as review-only oriented."),
        new("CODEX_MODE=implement", "CODEX_MODE=implement", "Marks the profile as implementation oriented."),
    };

    public PersonaEditorWindow(Persona persona)
    {
        InitializeComponent();
        Persona = Clone(persona);
        _templatesDir = ResolveTemplatesDir();
        InitializeChoices();
        LoadPersona();
    }

    private static string ResolveTemplatesDir()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var writable = Path.Combine(userProfile, ".codex-switcher", "templates", "personas");
        if (Directory.Exists(writable)) return writable;

        return Path.Combine(AppContext.BaseDirectory, "Templates", "personas");
    }

    private static Persona Clone(Persona p) => new()
    {
        Id = string.IsNullOrWhiteSpace(p.Id) ? Guid.NewGuid().ToString("N") : p.Id,
        Name = p.Name,
        Icon = string.IsNullOrWhiteSpace(p.Icon) ? "👤" : p.Icon,
        AgentsTemplatePath = p.AgentsTemplatePath,
        ApprovalsReviewer = string.IsNullOrWhiteSpace(p.ApprovalsReviewer) ? "user" : p.ApprovalsReviewer,
        ConfigOverrides = new Dictionary<string, string>(p.ConfigOverrides, StringComparer.OrdinalIgnoreCase),
        EnvVars = new Dictionary<string, string>(p.EnvVars, StringComparer.OrdinalIgnoreCase),
        CliArgs = p.CliArgs.ToList()
    };

    private void InitializeChoices()
    {
        BindChoices(IconBox, IconChoices, allowCurrentValue: true, currentValue: Persona.Icon);
        BindChoices(ModelBox, ModelChoices, allowCurrentValue: true, currentValue: GetConfig("model", "gpt-5.4"));
        BindChoices(ReasoningBox, ReasoningChoices, allowCurrentValue: true, currentValue: GetConfig("model_reasoning_effort", "high"));
        BindChoices(SandboxBox, SandboxChoices, allowCurrentValue: true, currentValue: GetConfig("sandbox_mode", "read-only"));
        BindChoices(ApprovalBox, ApprovalChoices, allowCurrentValue: true, currentValue: GetConfig("approval_policy", "on-request"));
        BindChoices(ApprovalsReviewerBox, ApprovalsReviewerChoices, allowCurrentValue: true, currentValue: GetApprovalsReviewer());
        BindChoices(TemplateBox, GetTemplateChoices(), allowCurrentValue: true, currentValue: Persona.AgentsTemplatePath);
        BindChoices(CliArgPresetBox, BuildCliArgChoices(), allowCurrentValue: false, currentValue: null);
        BindChoices(EnvVarPresetBox, BuildEnvVarChoices(), allowCurrentValue: false, currentValue: null);

        IconBox.SelectionChanged += ComboWithChoice_SelectionChanged;
        ModelBox.SelectionChanged += ComboWithChoice_SelectionChanged;
        ReasoningBox.SelectionChanged += ComboWithChoice_SelectionChanged;
        SandboxBox.SelectionChanged += ComboWithChoice_SelectionChanged;
        ApprovalBox.SelectionChanged += ComboWithChoice_SelectionChanged;
        ApprovalsReviewerBox.SelectionChanged += ComboWithChoice_SelectionChanged;
        TemplateBox.SelectionChanged += ComboWithChoice_SelectionChanged;
        CliArgPresetBox.SelectionChanged += ComboWithChoice_SelectionChanged;
        EnvVarPresetBox.SelectionChanged += ComboWithChoice_SelectionChanged;
    }

    private void LoadPersona()
    {
        NameBox.Text = Persona.Name;
        SelectChoice(IconBox, Persona.Icon, "👤");
        SelectChoice(ModelBox, GetConfig("model", "gpt-5.4"), "gpt-5.4");
        SelectChoice(ReasoningBox, GetConfig("model_reasoning_effort", "high"), "high");
        SelectChoice(SandboxBox, GetConfig("sandbox_mode", "read-only"), "read-only");
        SelectChoice(ApprovalBox, GetConfig("approval_policy", "on-request"), "on-request");
        SelectChoice(ApprovalsReviewerBox, GetApprovalsReviewer(), "user");
        _selectedTemplatePath = string.IsNullOrWhiteSpace(Persona.AgentsTemplatePath) ? "Templates/personas/custom_profile.md" : Persona.AgentsTemplatePath;
        RefreshTemplateChoices(_selectedTemplatePath);

        _cliArgs.Clear();
        _cliArgs.AddRange(Persona.CliArgs.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        RefreshCliArgsList();

        _envVars.Clear();
        foreach (var kv in Persona.EnvVars.Where(kv => !string.IsNullOrWhiteSpace(kv.Key)))
            _envVars[kv.Key.Trim()] = kv.Value ?? string.Empty;
        RefreshEnvVarsList();
    }

    private string GetConfig(string key, string fallback)
    {
        if (Persona.ConfigOverrides.TryGetValue(key, out var value)) return value;
        return key switch
        {
            "model_reasoning_effort" when Persona.ConfigOverrides.TryGetValue("reasoning_effort", out value) => value,
            "sandbox_mode" when Persona.ConfigOverrides.TryGetValue("sandbox", out value) => value,
            "approval_policy" when Persona.ConfigOverrides.TryGetValue("approval", out value) => value,
            _ => fallback
        };
    }

    private string GetApprovalsReviewer()
    {
        if (!string.IsNullOrWhiteSpace(Persona.ApprovalsReviewer))
        {
            var value = Persona.ApprovalsReviewer.Trim();
            if (string.Equals(value, "auto_review", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "guardian_subagent", StringComparison.OrdinalIgnoreCase))
                return "auto_review";
            return "user";
        }

        if (Persona.ConfigOverrides.TryGetValue("approvals_reviewer", out var overrideValue) && !string.IsNullOrWhiteSpace(overrideValue))
        {
            var normalized = overrideValue.Trim();
            if (string.Equals(normalized, "auto_review", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "guardian_subagent", StringComparison.OrdinalIgnoreCase))
                return "auto_review";
        }

        return "user";
    }

    private List<Choice> GetTemplateChoices()
    {
        var items = new List<Choice>();

        Directory.CreateDirectory(_templatesDir);

        foreach (var file in Directory.GetFiles(_templatesDir, "*.md")
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(file);
            var relative = "Templates/personas/" + name;
            items.Add(new Choice(relative, relative, "Role template file from the user templates/personas folder."));
        }

        if (items.Count == 0)
            items.Add(new Choice("Templates/personas/custom_profile.md", "Templates/personas/custom_profile.md", "Custom role template path. Click Linked to create it."));

        return items;
    }

    private List<Choice> BuildCliArgChoices()
    {
        var items = CliArgChoices.ToList();
        foreach (var arg in _cliArgs)
        {
            if (items.Any(x => string.Equals(x.Value, arg, StringComparison.OrdinalIgnoreCase))) continue;
            items.Add(new Choice(arg, arg, "Existing custom extra CLI arg preserved from this profile."));
        }
        return items;
    }

    private List<Choice> BuildEnvVarChoices()
    {
        var items = EnvVarChoices.ToList();
        foreach (var kv in _envVars)
        {
            var value = kv.Key + "=" + kv.Value;
            if (items.Any(x => string.Equals(x.Value, value, StringComparison.OrdinalIgnoreCase))) continue;
            items.Add(new Choice(value, value, "Existing custom environment variable preserved from this profile."));
        }
        return items;
    }

    private static void BindChoices(WpfComboBox box, IEnumerable<Choice> choices, bool allowCurrentValue, string? currentValue)
    {
        var list = choices.ToList();
        if (allowCurrentValue && !string.IsNullOrWhiteSpace(currentValue) &&
            !list.Any(x => string.Equals(x.Value, currentValue, StringComparison.OrdinalIgnoreCase)))
        {
            list.Insert(0, new Choice(currentValue, currentValue, "Existing value preserved from this profile."));
        }

        box.ItemsSource = list;
        box.SelectedValuePath = nameof(Choice.Value);
        TextSearch.SetTextPath(box, nameof(Choice.Label));
        box.IsTextSearchEnabled = true;
        if (list.Count > 0) box.SelectedIndex = 0;
    }

    private static void SelectChoice(WpfComboBox box, string? value, string? fallback)
    {
        var target = string.IsNullOrWhiteSpace(value) ? fallback : value;
        if (string.IsNullOrWhiteSpace(target))
        {
            if (box.Items.Count > 0) box.SelectedIndex = 0;
            return;
        }

        box.SelectedValue = target;
        if (box.SelectedIndex < 0 && box.Items.Count > 0)
            box.SelectedIndex = 0;
    }

    private static string ChoiceValue(WpfComboBox box, string fallback)
    {
        if (box.SelectedItem is Choice choice && !string.IsNullOrWhiteSpace(choice.Value))
            return choice.Value;

        return box.SelectedValue?.ToString()?.Trim() ?? fallback;
    }

    private void RefreshCliArgsList()
    {
        CliArgsList.ItemsSource = null;
        CliArgsList.ItemsSource = _cliArgs.ToList();
        BindChoices(CliArgPresetBox, BuildCliArgChoices(), allowCurrentValue: false, currentValue: null);
    }

    private void RefreshEnvVarsList()
    {
        EnvVarsList.ItemsSource = null;
        EnvVarsList.ItemsSource = _envVars.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                                          .Select(kv => kv.Key + "=" + kv.Value)
                                          .ToList();
        BindChoices(EnvVarPresetBox, BuildEnvVarChoices(), allowCurrentValue: false, currentValue: null);
    }

    private static KeyValuePair<string, string>? ParseEnvVarPair(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var idx = value.IndexOf('=');
        if (idx <= 0) return null;
        var key = value[..idx].Trim();
        var val = value[(idx + 1)..];
        if (string.IsNullOrWhiteSpace(key)) return null;
        return new KeyValuePair<string, string>(key, val);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            WpfMessageBox.Show("Profile name is required.", "Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Persona.Name = NameBox.Text.Trim();
        Persona.Icon = ChoiceValue(IconBox, "👤");
        Persona.AgentsTemplatePath = ChoiceValue(TemplateBox, string.IsNullOrWhiteSpace(_selectedTemplatePath) ? "Templates/personas/custom_profile.md" : _selectedTemplatePath);
        Persona.ApprovalsReviewer = ChoiceValue(ApprovalsReviewerBox, "user");
        Persona.ConfigOverrides["model"] = ChoiceValue(ModelBox, "gpt-5.4");
        Persona.ConfigOverrides["model_reasoning_effort"] = ChoiceValue(ReasoningBox, "high");
        Persona.ConfigOverrides["sandbox_mode"] = ChoiceValue(SandboxBox, "read-only");
        Persona.ConfigOverrides["approval_policy"] = ChoiceValue(ApprovalBox, "on-request");
        Persona.ConfigOverrides.Remove("approvals_reviewer");
        Persona.ConfigOverrides.Remove("reasoning_effort");
        Persona.ConfigOverrides.Remove("sandbox");
        Persona.ConfigOverrides.Remove("approval");
        Persona.CliArgs = _cliArgs.ToList();
        Persona.EnvVars = new Dictionary<string, string>(_envVars, StringComparer.OrdinalIgnoreCase);
        DialogResult = true;
        Close();
    }

    private void AddCliArg_Click(object sender, RoutedEventArgs e)
    {
        var value = ChoiceValue(CliArgPresetBox, string.Empty);
        if (string.IsNullOrWhiteSpace(value)) return;
        if (_cliArgs.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase))) return;
        _cliArgs.Add(value);
        RefreshCliArgsList();
    }

    private void RemoveCliArg_Click(object sender, RoutedEventArgs e)
    {
        if (CliArgsList.SelectedItem is not string selected || string.IsNullOrWhiteSpace(selected)) return;
        _cliArgs.RemoveAll(x => string.Equals(x, selected, StringComparison.OrdinalIgnoreCase));
        RefreshCliArgsList();
    }

    private void AddEnvVar_Click(object sender, RoutedEventArgs e)
    {
        var selected = ChoiceValue(EnvVarPresetBox, string.Empty);
        var pair = ParseEnvVarPair(selected);
        if (pair == null) return;
        _envVars[pair.Value.Key] = pair.Value.Value;
        RefreshEnvVarsList();
    }

    private void RemoveEnvVar_Click(object sender, RoutedEventArgs e)
    {
        if (EnvVarsList.SelectedItem is not string selected || string.IsNullOrWhiteSpace(selected)) return;
        var pair = ParseEnvVarPair(selected);
        if (pair == null) return;
        _envVars.Remove(pair.Value.Key);
        RefreshEnvVarsList();
    }

    private void ComboWithChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not WpfComboBox box || box.SelectedItem is not Choice choice) return;
        box.ToolTip = choice.Description;

        if (ReferenceEquals(box, TemplateBox))
            _selectedTemplatePath = choice.Value;
    }

    private void OpenTemplatesFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_templatesDir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _templatesDir,
            UseShellExecute = true
        });
    }

    private void OpenLinkedTemplate_Click(object sender, RoutedEventArgs e)
    {
        var path = EnsureLinkedTemplateExists();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
        RefreshTemplateChoices(ChoiceValue(TemplateBox, _selectedTemplatePath));
    }

    private void RequestInstructionsRefresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshInstructionsRequested = true;
        WpfMessageBox.Show(
            "Instructions refresh requested. Save this profile, then the main window will regenerate config.toml profile references, profile instruction files, and compact AGENTS.md for all accounts.",
            "Refresh Instructions",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private string EnsureLinkedTemplateExists()
    {
        var selected = ChoiceValue(TemplateBox, _selectedTemplatePath);
        if (string.IsNullOrWhiteSpace(selected))
            selected = "Templates/personas/custom_profile.md";

        var relative = selected.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (relative.StartsWith("Templates" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            relative = relative[("Templates" + Path.DirectorySeparatorChar).Length..];

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex-switcher",
            "templates",
            relative);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            File.WriteAllText(path,
                "# CEM Role: " + NameBox.Text.Trim() + Environment.NewLine + Environment.NewLine +
                "## Mission" + Environment.NewLine +
                "Define the purpose of this profile." + Environment.NewLine + Environment.NewLine +
                "## Scope" + Environment.NewLine +
                "- Describe allowed behavior." + Environment.NewLine +
                "- Describe limitations." + Environment.NewLine + Environment.NewLine +
                "## Workflow" + Environment.NewLine +
                "1. Read the active launch context." + Environment.NewLine +
                "2. Follow this role template." + Environment.NewLine);
        }

        return path;
    }

    private void RefreshTemplates_Click(object sender, RoutedEventArgs e)
    {
        RefreshTemplateChoices(ChoiceValue(TemplateBox, _selectedTemplatePath));
    }

    private void RefreshTemplateChoices(string? preferredValue)
    {
        var value = string.IsNullOrWhiteSpace(preferredValue) ? _selectedTemplatePath : preferredValue;
        BindChoices(TemplateBox, GetTemplateChoices(), allowCurrentValue: true, currentValue: value);
        SelectChoice(TemplateBox, value, "Templates/personas/custom_profile.md");
        _selectedTemplatePath = ChoiceValue(TemplateBox, "Templates/personas/custom_profile.md");
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
