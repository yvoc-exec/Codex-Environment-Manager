using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CodexEnvironmentManager.Models;
using CodexEnvironmentManager.Services;
using WpfMessageBox = System.Windows.MessageBox;

namespace CodexEnvironmentManager.Views;

public partial class AccountWizardWindow : Window
{
    private readonly AccountManager _accountManager;

    private sealed class ProviderChoice
    {
        public string Value { get; }
        public string Label { get; }
        public ProviderChoice(string value, string label) { Value = value; Label = label; }
        public override string ToString() => Label;
    }

    private sealed class AuthChoice
    {
        public string Value { get; }
        public string Label { get; }
        public string Provider { get; }
        public bool RequiresApiKey { get; }
        public AuthChoice(string value, string label, string provider, bool requiresApiKey)
        { Value = value; Label = label; Provider = provider; RequiresApiKey = requiresApiKey; }
        public override string ToString() => Label;
    }

    private static readonly ProviderChoice[] Providers =
    {
        new("codex", "Codex"),
        new("kimi", "Kimi")
    };

    private static readonly AuthChoice[] AuthTypes =
    {
        new("plus", "Plus (OAuth)", "codex", false),
        new("api_key", "API Key", "codex", true),
        new("kimi_oauth", "Kimi Code (OAuth)", "kimi", false),
        new("moonshot_api_key", "Moonshot API Key", "kimi", true),
    };

    public Account? CreatedAccount { get; private set; }

    public AccountWizardWindow(AccountManager accountManager)
    {
        InitializeComponent();
        _accountManager = accountManager;
        InitializeChoices();
    }

    private void InitializeChoices()
    {
        ProviderBox.ItemsSource = Providers.ToList();
        ProviderBox.SelectedValuePath = nameof(ProviderChoice.Value);
        ProviderBox.DisplayMemberPath = nameof(ProviderChoice.Label);
        ProviderBox.SelectedIndex = 0;

        AuthTypeBox.ItemsSource = AuthTypes.Where(a => a.Provider == "codex").ToList();
        AuthTypeBox.SelectedValuePath = nameof(AuthChoice.Value);
        AuthTypeBox.DisplayMemberPath = nameof(AuthChoice.Label);
        AuthTypeBox.SelectedIndex = 0;

        ProviderBox.SelectionChanged += ProviderBox_SelectionChanged;
        AuthTypeBox.SelectionChanged += AuthTypeBox_SelectionChanged;
        UpdateUi();
    }

    private void ProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var provider = (ProviderBox.SelectedItem as ProviderChoice)?.Value ?? "codex";
        AuthTypeBox.ItemsSource = AuthTypes.Where(a => a.Provider == provider).ToList();
        AuthTypeBox.SelectedIndex = 0;
        UpdateUi();
    }

    private void AuthTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateUi();
    }

    private void UpdateUi()
    {
        var auth = AuthTypeBox.SelectedItem as AuthChoice;
        var requiresKey = auth?.RequiresApiKey ?? false;
        ApiKeyLabel.Visibility = requiresKey ? Visibility.Visible : Visibility.Collapsed;
        ApiKeyBox.Visibility = requiresKey ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText.Text = "Account name is required.";
            return;
        }

        var auth = AuthTypeBox.SelectedItem as AuthChoice;
        if (auth == null)
        {
            StatusText.Text = "Select an authentication type.";
            return;
        }

        try
        {
            if (auth.RequiresApiKey)
            {
                var apiKey = ApiKeyBox.Password.Trim();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    StatusText.Text = "API key is required for this account type.";
                    return;
                }

                if (auth.Provider == "kimi")
                {
                    _accountManager.AddKimiApiKeyAccount(name, apiKey);
                }
                else
                {
                    _accountManager.AddApiKeyAccount(name, apiKey);
                }
            }
            else
            {
                if (auth.Provider == "kimi")
                {
                    _accountManager.AddKimiOAuthAccount(name);
                }
                else
                {
                    _accountManager.AddPlusAccount(name);
                }
            }

            var accounts = _accountManager.GetAccounts();
            CreatedAccount = accounts.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
