using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace WindowsTools.Controls;

/// <summary>
/// Launches an external program and reparents its top-level window into the WPF
/// UI (Win32 SetParent). Used to host the real Windows "Settings" Windows Update
/// page inside the app.
/// </summary>
public class EmbeddedWindowHost : HwndHost
{
    private const int GWL_STYLE = -16;
    private const int WS_CHILD = unchecked((int)0x40000000);
    private const int WS_VISIBLE = unchecked((int)0x10000000);
    private const int WS_CAPTION = unchecked((int)0x00C00000);
    private const int WS_THICKFRAME = unchecked((int)0x00040000);
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_BORDER = unchecked((int)0x00800000);
    private const int WS_DLGFRAME = unchecked((int)0x00400000);
    private const int WS_SYSMENU = unchecked((int)0x00080000);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(int exStyle, string cls, string? name, int style,
        int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int w, int h, bool repaint);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out RECT r);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder s, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder s, int max);

    private delegate bool EnumProc(IntPtr hwnd, IntPtr p);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private readonly Action _launch;
    private readonly Func<IntPtr> _find;
    private IntPtr _hostHwnd;
    private IntPtr _appHwnd;
    private int _originalStyle;

    public EmbeddedWindowHost(Action launch, Func<IntPtr> find)
    {
        _launch = launch;
        _find = find;
    }

    /// <summary>Finds a visible top-level window by class and title.</summary>
    public static IntPtr FindWindowByClassTitle(string className, string titleEquals)
    {
        var found = IntPtr.Zero;
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            var cls = new StringBuilder(256);
            GetClassName(h, cls, cls.Capacity);
            if (cls.ToString() != className) return true;
            var title = new StringBuilder(256);
            GetWindowText(h, title, title.Capacity);
            if (title.ToString() == titleEquals) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _hostHwnd = CreateWindowEx(0, "static", null, WS_CHILD | WS_VISIBLE,
            0, 0, 0, 0, hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        _ = EmbedAsync();
        return new HandleRef(this, _hostHwnd);
    }

    private async Task EmbedAsync()
    {
        await Task.Run(_launch);

        var h = IntPtr.Zero;
        for (var i = 0; i < 150 && h == IntPtr.Zero; i++)
        {
            h = _find();
            if (h == IntPtr.Zero) await Task.Delay(150);
        }
        if (h == IntPtr.Zero) return;

        Dispatcher.Invoke(() =>
        {
            _appHwnd = h;
            _originalStyle = GetWindowLong(_appHwnd, GWL_STYLE);
            var style = (_originalStyle & ~(WS_CAPTION | WS_THICKFRAME | WS_POPUP | WS_BORDER | WS_DLGFRAME | WS_SYSMENU)) | WS_CHILD;
            SetWindowLong(_appHwnd, GWL_STYLE, style);
            SetParent(_appHwnd, _hostHwnd);
            ShowWindow(_appHwnd, 1);
            ResizeChild();
        });
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        ResizeChild();
    }

    private void ResizeChild()
    {
        if (_appHwnd == IntPtr.Zero || _hostHwnd == IntPtr.Zero) return;
        if (GetClientRect(_hostHwnd, out var r))
            MoveWindow(_appHwnd, 0, 0, r.Right - r.Left, r.Bottom - r.Top, true);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (_appHwnd != IntPtr.Zero)
        {
            // Return the window to a normal floating state instead of destroying it.
            try
            {
                SetParent(_appHwnd, IntPtr.Zero);
                SetWindowLong(_appHwnd, GWL_STYLE, _originalStyle);
            }
            catch { }
            _appHwnd = IntPtr.Zero;
        }
        if (_hostHwnd != IntPtr.Zero) { DestroyWindow(_hostHwnd); _hostHwnd = IntPtr.Zero; }
    }
}
