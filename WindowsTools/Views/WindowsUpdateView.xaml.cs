using System.Windows;
using System.Windows.Controls;
using WindowsTools.Controls;
using WindowsTools.Services;

namespace WindowsTools.Views;

public partial class WindowsUpdateView : UserControl
{
    private bool _ran;

    public WindowsUpdateView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) =>
        {
            if (EmbedHost.Child is EmbeddedWindowHost host) host.Dispose();
            EmbedHost.Child = null;
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_ran) return;
        _ran = true;

        // Arm/disarm the one-time reopen-after-restart based on whether Windows
        // is currently waiting on a restart to finish updates.
        if (WindowsUpdateService.IsRebootPending())
            WindowsUpdateService.ArmAutoOpen();
        else
            WindowsUpdateService.DisarmAutoOpen();

        // Host the real Settings "Windows Update" window inside the app.
        EmbedHost.Child = new EmbeddedWindowHost(
            WindowsUpdateService.OpenWindowsUpdate,
            () => EmbeddedWindowHost.FindWindowByClassTitle("ApplicationFrameWindow", "Settings"));
    }
}
