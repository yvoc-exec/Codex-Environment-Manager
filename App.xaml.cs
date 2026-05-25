using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace CodexEnvironmentManager;

public partial class App : WpfApplication
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        RegisterGlobalExceptionHandlers();
        WriteStartupTrace("Application startup requested.");

        _singleInstanceMutex = new Mutex(true, @"Local\CodexEnvironmentManager_App", out var createdNew);
        _ownsSingleInstanceMutex = createdNew;
        if (!createdNew)
        {
            WpfMessageBox.Show("Codex Environment Manager is already running. Check the notification tray/system tray.", "Already Running", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        try
        {
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
            WriteStartupTrace("MainWindow shown.");
        }
        catch (Exception ex)
        {
            WriteFatal(ex, "Startup failed before MainWindow could be shown.");
            WpfMessageBox.Show($"Codex Environment Manager failed to start:{Environment.NewLine}{Environment.NewLine}{ex.Message}{Environment.NewLine}{Environment.NewLine}Details were written to:{Environment.NewLine}{FatalLogPath}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (Current.MainWindow is MainWindow mw)
                mw.DisposeTray();
            if (_ownsSingleInstanceMutex)
                _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            WriteStartupTrace($"Application exited with code {e.ApplicationExitCode}.");
        }
        catch
        {
            // Avoid throwing from shutdown cleanup.
        }
        base.OnExit(e);
    }

    private static string BaseDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex-switcher");
    private static string LogDir => Path.Combine(BaseDir, "logs");
    private static string FatalLogPath => Path.Combine(LogDir, "fatal-startup.log");

    private static void RegisterGlobalExceptionHandlers()
    {
        Current.DispatcherUnhandledException += (_, args) =>
        {
            WriteFatal(args.Exception, "Dispatcher unhandled exception.");
            WpfMessageBox.Show($"Unexpected UI error:{Environment.NewLine}{Environment.NewLine}{args.Exception.Message}{Environment.NewLine}{Environment.NewLine}Details were written to:{Environment.NewLine}{FatalLogPath}", "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                WriteFatal(ex, "AppDomain unhandled exception.");
            else
                WriteStartupTrace($"AppDomain unhandled exception object: {args.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteFatal(args.Exception, "Unobserved task exception.");
            args.SetObserved();
        };
    }

    private static void WriteFatal(Exception ex, string context)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            File.AppendAllText(FatalLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Last-resort logging must never crash the app.
        }
    }

    private static void WriteStartupTrace(string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            File.AppendAllText(Path.Combine(LogDir, "startup.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging failures.
        }
    }
}
