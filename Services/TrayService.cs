using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace CodexEnvironmentManager.Services;

public class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly MainWindow _window;

    public TrayService(MainWindow window)
    {
        _window = window;
        var appIcon = LoadAppIcon();
        _icon = new NotifyIcon
        {
            Icon = appIcon ?? SystemIcons.Application,
            Text = "Codex Environment Manager",
            Visible = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show", null, (s, e) => ShowWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (s, e) =>
        {
            _icon.Visible = false;
            _window.RequestExit();
        });
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (s, e) => ShowWindow();
    }

    private static Icon? LoadAppIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/AppIcon.ico", UriKind.Absolute);
            var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
            if (stream != null)
                return new Icon(stream);
        }
        catch
        {
            // Fall back to generic system icon.
        }
        return null;
    }

    private void ShowWindow()
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
