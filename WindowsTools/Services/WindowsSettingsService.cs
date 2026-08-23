using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Threading;

namespace WindowsTools.Services;

/// <summary>
/// Tracks external Settings windows opened by Windows Tools so they can be
/// closed before the application starts its own installer.
/// </summary>
public sealed class WindowsSettingsService : IDisposable
{
    private const int WM_CLOSE = 0x0010;

    private readonly DispatcherTimer _timer;
    private readonly HashSet<IntPtr> _ownedExternalWindows = new();
    private IntPtr _settingsWindow;
    private bool _disposed;

    public WindowsSettingsService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _timer.Tick += CheckSettingsPage;
    }

    public void OpenWindowsUpdate()
    {
        StopWatching();

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = "ms-settings:windowsupdate",
            UseShellExecute = true
        });

        // Give Settings a moment to create its host window, then track it.
        _timer.Start();
    }

    /// <summary>
    /// Closes every external window that Windows Tools has opened and is
    /// currently tracking. This must be called before launching our updater so
    /// an external Settings window cannot keep the application files in use.
    /// </summary>
    public void CloseExternalWindows()
    {
        foreach (var hWnd in _ownedExternalWindows.ToArray())
        {
            if (IsWindow(hWnd))
                CloseWindow(hWnd);
        }

        _ownedExternalWindows.Clear();
        _settingsWindow = IntPtr.Zero;
        _timer.Stop();
    }

    private void CheckSettingsPage(object? sender, EventArgs e)
    {
        if (_settingsWindow == IntPtr.Zero)
        {
            _settingsWindow = FindSettingsWindow();
            if (_settingsWindow != IntPtr.Zero)
                _ownedExternalWindows.Add(_settingsWindow);
            return;
        }

        if (!IsWindow(_settingsWindow))
        {
            _ownedExternalWindows.Remove(_settingsWindow);
            StopWatching();
            return;
        }

        try
        {
            var root = AutomationElement.FromHandle(_settingsWindow);
            if (root == null)
                return;

            if (!IsWindowsUpdateSelected(root))
            {
                CloseWindow(_settingsWindow);
                _ownedExternalWindows.Remove(_settingsWindow);
                StopWatching();
            }
        }
        catch
        {
            // Settings can temporarily rebuild its UI while navigating.
        }
    }

    private static bool IsWindowsUpdateSelected(AutomationElement root)
    {
        var condition = new PropertyCondition(
            AutomationElement.ControlTypeProperty,
            ControlType.ListItem);

        foreach (AutomationElement item in root.FindAll(TreeScope.Descendants, condition))
        {
            if (!string.Equals(item.Current.Name, "Windows Update", StringComparison.OrdinalIgnoreCase))
                continue;

            if (item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern))
                return ((SelectionItemPattern)pattern).Current.IsSelected;
        }

        return true;
    }

    private static IntPtr FindSettingsWindow()
    {
        IntPtr found = IntPtr.Zero;

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            GetWindowThreadProcessId(hWnd, out var processId);
            try
            {
                using var process = Process.GetProcessById((int)processId);
                if (!string.Equals(process.ProcessName, "ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(process.ProcessName, "SystemSettings", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
                return true;
            }

            try
            {
                var root = AutomationElement.FromHandle(hWnd);
                if (root != null && IsWindowsUpdateSelected(root))
                {
                    found = hWnd;
                    return false;
                }
            }
            catch
            {
                // Ignore windows that are still initializing.
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    private void StopWatching()
    {
        _timer.Stop();
        _settingsWindow = IntPtr.Zero;
    }

    private static void CloseWindow(IntPtr hWnd) => PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseExternalWindows();
        _timer.Tick -= CheckSettingsPage;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}
