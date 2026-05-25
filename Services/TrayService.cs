using System;
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
        _icon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
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
