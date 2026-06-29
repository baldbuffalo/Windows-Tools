using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WindowsTools.Controls;

/// <summary>
/// Hosts an external program's main window inside the WPF UI by reparenting it
/// (Win32 SetParent). Works for normal Win32 apps; Store/UWP apps generally
/// can't be reparented and should be launched externally instead.
/// </summary>
public class EmbeddedAppHost : HwndHost
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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private readonly Func<Process?> _launch;
    private IntPtr _hostHwnd;
    private IntPtr _appHwnd;
    private int _originalStyle;
    private Process? _process;

    public EmbeddedAppHost(Func<Process?> launch) => _launch = launch;

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _hostHwnd = CreateWindowEx(0, "static", null, WS_CHILD | WS_VISIBLE,
            0, 0, 0, 0, hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        _ = EmbedAsync();
        return new HandleRef(this, _hostHwnd);
    }

    private async Task EmbedAsync()
    {
        _process = await Task.Run(_launch);
        if (_process is null) return;

        var h = IntPtr.Zero;
        for (var i = 0; i < 150 && h == IntPtr.Zero; i++)
        {
            try { _process.Refresh(); h = _process.MainWindowHandle; } catch { }
            if (h == IntPtr.Zero) await Task.Delay(200);
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
        // Detach the app back to a normal floating window, then close it.
        if (_appHwnd != IntPtr.Zero)
        {
            try
            {
                SetParent(_appHwnd, IntPtr.Zero);
                SetWindowLong(_appHwnd, GWL_STYLE, _originalStyle);
            }
            catch { }
            try { if (_process is { HasExited: false }) _process.CloseMainWindow(); } catch { }
            _appHwnd = IntPtr.Zero;
        }
        if (_hostHwnd != IntPtr.Zero) { DestroyWindow(_hostHwnd); _hostHwnd = IntPtr.Zero; }
    }
}
