using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using WinFormsClipboard = System.Windows.Forms.Clipboard;
using WinFormsSendKeys = System.Windows.Forms.SendKeys;
using UIA = System.Windows.Automation;

namespace CodexEnvironmentManager.Services;

/// <summary>
/// Windows-only helper for the Microsoft Store/AppX Codex Desktop path.
///
/// Current Codex CLI behavior on Windows can launch the Desktop app with `codex app`,
/// but it does not reliably import/open the selected workspace path in the Desktop UI.
///
/// This bridge is intentionally layered:
/// 1. Wait for/focus Codex Desktop.
/// 2. If a new account shows the "Set up Agent sandbox" gate, click Set up first.
/// 3. Prefer UI Automation to invoke visible controls such as "Work in a project".
/// 4. Fall back to keyboard shortcuts/SendKeys and standard folder-picker automation.
/// </summary>
public static class DesktopWorkspaceBridge
{
    private const int SwRestore = 9;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    public static void ScheduleOpenWorkspace(string workspacePath, LogService log)
    {
        if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
        {
            log.Warn($"Desktop workspace bridge skipped; workspace path missing: {workspacePath}");
            return;
        }

        var thread = new Thread(() => TryOpenWorkspace(workspacePath, log))
        {
            IsBackground = true,
            Name = "Codex Desktop Workspace Bridge"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private static void TryOpenWorkspace(string workspacePath, LogService log)
    {
        try
        {
            log.Info($"Desktop workspace bridge scheduled for: {workspacePath}");
            Thread.Sleep(3500);

            var hwnd = WaitForCodexWindow(TimeSpan.FromSeconds(25), log);
            if (hwnd == IntPtr.Zero)
            {
                log.Warn("Desktop workspace bridge could not find a Codex Desktop window.");
                return;
            }

            FocusWindow(hwnd);
            WinFormsClipboard.SetText(workspacePath);

            // New/empty Codex Desktop accounts can show a blocking setup gate before
            // project selection works. Click it when present, then wait for the UI to settle.
            if (TryInvokeNamedElement(hwnd, new[] { "Set up", "Set up Agent sandbox" }, log, TimeSpan.FromSeconds(5)))
            {
                log.Info("Desktop workspace bridge clicked Codex Agent sandbox setup gate.");
                Thread.Sleep(10000);
                hwnd = WaitForCodexWindow(TimeSpan.FromSeconds(25), log);
                if (hwnd == IntPtr.Zero)
                {
                    log.Warn("Desktop workspace bridge lost Codex window after sandbox setup attempt.");
                    return;
                }
                FocusWindow(hwnd);
                WinFormsClipboard.SetText(workspacePath);
            }

            // First try Codex's own project selector using UI Automation. This is more
            // reliable than assuming Ctrl+O exists on Windows.
            var openedProjectControl = TryInvokeNamedElement(
                hwnd,
                new[] { "Work in a project", "Project", "Open project", "Choose project", "Select project" },
                log,
                TimeSpan.FromSeconds(7));

            if (openedProjectControl)
            {
                Thread.Sleep(1000);

                // Some builds open an in-app menu first. Click a likely local-folder item.
                TryInvokeNamedElement(
                    IntPtr.Zero,
                    new[] { "Open folder", "Open local folder", "Choose folder", "Select folder", "Browse", "Add project", "Open workspace" },
                    log,
                    TimeSpan.FromSeconds(4));
            }
            else
            {
                log.Warn("Desktop workspace bridge did not find project selector by UIA; trying keyboard fallback.");
                FocusWindow(hwnd);
                WinFormsSendKeys.SendWait("^o");
                Thread.Sleep(1000);
            }

            if (TryDriveFolderDialog(workspacePath, log, TimeSpan.FromSeconds(10)))
            {
                log.Info($"Desktop workspace bridge selected workspace through folder dialog: {workspacePath}");
                return;
            }

            // Last-resort keyboard path. It helps when the app focuses an editable path field,
            // and is harmless if nothing editable is focused.
            FocusWindow(hwnd);
            WinFormsClipboard.SetText(workspacePath);
            WinFormsSendKeys.SendWait("^v");
            Thread.Sleep(300);
            WinFormsSendKeys.SendWait("{ENTER}");
            Thread.Sleep(800);
            WinFormsSendKeys.SendWait("{ENTER}");

            log.Warn($"Desktop workspace bridge made a best-effort project-open attempt, but no standard folder dialog was confirmed: {workspacePath}");
        }
        catch (Exception ex)
        {
            log.Warn($"Desktop workspace bridge failed: {ex.Message}");
        }
    }

    private static void FocusWindow(IntPtr hwnd)
    {
        ShowWindow(hwnd, SwRestore);
        SetForegroundWindow(hwnd);
        Thread.Sleep(600);
    }

    private static bool TryDriveFolderDialog(string workspacePath, LogService log, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var dialog = FindLikelyFolderDialog();
                if (dialog != null)
                {
                    log.Info("Desktop workspace bridge found a folder/open dialog.");

                    // The address bar trick is reliable across modern Windows file/folder dialogs.
                    WinFormsClipboard.SetText(workspacePath);
                    WinFormsSendKeys.SendWait("%d");
                    Thread.Sleep(250);
                    WinFormsSendKeys.SendWait("^v");
                    Thread.Sleep(250);
                    WinFormsSendKeys.SendWait("{ENTER}");
                    Thread.Sleep(1000);
                    WinFormsSendKeys.SendWait("{ENTER}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                log.Warn($"Folder dialog automation attempt failed: {ex.Message}");
            }

            Thread.Sleep(400);
        }

        return false;
    }

    private static UIA.AutomationElement? FindLikelyFolderDialog()
    {
        var root = UIA.AutomationElement.RootElement;
        var windows = root.FindAll(UIA.TreeScope.Children, UIA.Condition.TrueCondition);
        foreach (UIA.AutomationElement window in windows)
        {
            try
            {
                var name = window.Current.Name ?? string.Empty;
                var className = window.Current.ClassName ?? string.Empty;
                if (className.Equals("#32770", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Select", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Open", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Folder", StringComparison.OrdinalIgnoreCase))
                {
                    return window;
                }
            }
            catch
            {
                // Ignore stale UIA elements.
            }
        }

        return null;
    }

    private static bool TryInvokeNamedElement(IntPtr hwnd, string[] names, LogService log, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var root = hwnd == IntPtr.Zero ? UIA.AutomationElement.RootElement : UIA.AutomationElement.FromHandle(hwnd);
                var element = FindElementByName(root, names);
                if (element != null)
                {
                    var elementName = SafeElementName(element);
                    if (InvokeOrClick(element))
                    {
                        log.Info($"Desktop workspace bridge invoked UI element: {elementName}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn($"UI Automation search failed: {ex.Message}");
            }

            Thread.Sleep(350);
        }

        return false;
    }

    private static UIA.AutomationElement? FindElementByName(UIA.AutomationElement root, string[] names)
    {
        var all = root.FindAll(UIA.TreeScope.Descendants, UIA.Condition.TrueCondition);
        UIA.AutomationElement? best = null;

        foreach (UIA.AutomationElement el in all)
        {
            try
            {
                var currentName = el.Current.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(currentName)) continue;
                if (names.Any(n => currentName.Equals(n, StringComparison.OrdinalIgnoreCase)))
                    return el;
                if (best == null && names.Any(n => currentName.Contains(n, StringComparison.OrdinalIgnoreCase)))
                    best = el;
            }
            catch
            {
                // Ignore stale/unavailable UIA elements.
            }
        }

        return best;
    }

    private static bool InvokeOrClick(UIA.AutomationElement element)
    {
        try
        {
            if (element.TryGetCurrentPattern(UIA.InvokePattern.Pattern, out var pattern) && pattern is UIA.InvokePattern invoke)
            {
                invoke.Invoke();
                return true;
            }
        }
        catch
        {
            // Fall back to mouse click.
        }

        try
        {
            var rect = element.Current.BoundingRectangle;
            if (!rect.IsEmpty && rect.Width > 1 && rect.Height > 1)
            {
                var x = (int)(rect.Left + rect.Width / 2);
                var y = (int)(rect.Top + rect.Height / 2);
                SetCursorPos(x, y);
                mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
                mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
                return true;
            }
        }
        catch
        {
            // Ignore; caller will continue fallback strategy.
        }

        return false;
    }

    private static string SafeElementName(UIA.AutomationElement element)
    {
        try { return element.Current.Name ?? "<unnamed>"; }
        catch { return "<stale>"; }
    }

    private static IntPtr WaitForCodexWindow(TimeSpan timeout, LogService log)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var hwnd = FindCodexWindow();
            if (hwnd != IntPtr.Zero) return hwnd;
            Thread.Sleep(500);
        }
        return IntPtr.Zero;
    }

    private static IntPtr FindCodexWindow()
    {
        foreach (var proc in Process.GetProcesses().OrderByDescending(p => SafeStartTimeTicks(p)))
        {
            try
            {
                if (proc.MainWindowHandle == IntPtr.Zero) continue;
                var name = proc.ProcessName ?? string.Empty;
                var title = proc.MainWindowTitle ?? string.Empty;
                if (name.Contains("Codex", StringComparison.OrdinalIgnoreCase) ||
                    title.Contains("Codex", StringComparison.OrdinalIgnoreCase))
                {
                    return proc.MainWindowHandle;
                }
            }
            catch
            {
                // Ignore processes we cannot inspect.
            }
        }
        return IntPtr.Zero;
    }

    private static long SafeStartTimeTicks(Process p)
    {
        try { return p.StartTime.Ticks; }
        catch { return 0; }
    }
}
