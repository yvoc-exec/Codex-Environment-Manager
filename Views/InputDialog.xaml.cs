using System.Windows;

namespace CodexEnvironmentManager.Views;

public partial class InputDialog : Window
{
    private readonly bool _isSecret;
    public string ResponseText { get; private set; } = "";

    public InputDialog(string prompt, string defaultText = "", bool isSecret = false)
    {
        InitializeComponent();
        _isSecret = isSecret;
        PromptText.Text = prompt;
        if (isSecret)
        {
            InputText.Visibility = Visibility.Collapsed;
            SecretInput.Visibility = Visibility.Visible;
            SecretInput.Password = defaultText;
            Loaded += (_, _) => SecretInput.Focus();
        }
        else
        {
            InputText.Text = defaultText;
            Loaded += (_, _) => InputText.Focus();
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ResponseText = _isSecret ? SecretInput.Password : InputText.Text;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
