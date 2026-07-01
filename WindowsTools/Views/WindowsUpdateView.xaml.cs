using System.Windows;
using System.Windows.Controls;
using WindowsTools.Services;

namespace WindowsTools.Views;

public partial class WindowsUpdateView : UserControl
{
    private bool _ran;

    public WindowsUpdateView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_ran) return;
        _ran = true;

        if (WindowsUpdateService.IsRebootPending())
        {
            // A restart is needed — arm the app to reopen here once it's done,
            // no matter how the user restarts.
            WindowsUpdateService.ArmAutoOpen();
            StatusText.Text = "A restart is required to finish installing updates. " +
                              "Windows Tools will reopen here automatically after you restart.";
            RestartButton.Visibility = Visibility.Visible;
        }
        else
        {
            // Nothing pending — make sure we don't reopen here next logon.
            WindowsUpdateService.DisarmAutoOpen();
            StatusText.Text = "Check for and install Windows updates below.";
            RestartButton.Visibility = Visibility.Collapsed;
        }

        WindowsUpdateService.OpenWindowsUpdate();
    }

    private void OpenUpdate_Click(object sender, RoutedEventArgs e) => WindowsUpdateService.OpenWindowsUpdate();

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        WindowsUpdateService.ArmAutoOpen();
        WindowsUpdateService.RestartNow();
    }
}
