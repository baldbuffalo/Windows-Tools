using System.Windows;
using System.Windows.Controls;
using WindowsTools.Services;

namespace WindowsTools.Views;

public partial class WindowsUpdateView : UserControl
{
    private List<WindowsUpdateItem> _updates = [];
    private bool _ran;
    private bool _busy;

    public WindowsUpdateView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_ran) return;
        _ran = true;

        // If Windows is already waiting on a restart, arm the one-time reopen so
        // the app comes back here once the restart is done (any restart method).
        if (WindowsUpdateService.IsRebootPending())
        {
            WindowsUpdateService.ArmAutoOpen();
            StatusText.Text = "A restart is required to finish installing updates. " +
                              "Windows Tools will reopen here after you restart.";
            RestartButton.Visibility = Visibility.Visible;
            return;
        }

        WindowsUpdateService.DisarmAutoOpen();
        await ScanAsync();
    }

    private async void Scan_Click(object sender, RoutedEventArgs e) => await ScanAsync();

    private async Task ScanAsync()
    {
        if (_busy) return;
        SetBusy(true);
        InstallButton.Visibility = Visibility.Collapsed;
        RestartButton.Visibility = Visibility.Collapsed;
        UpdateList.ItemsSource = null;
        StatusText.Text = "Checking for updates...";

        try
        {
            _updates = await WindowsUpdateService.ScanAsync();
            UpdateList.ItemsSource = _updates;
            if (_updates.Count == 0)
            {
                StatusText.Text = "You're up to date.";
            }
            else
            {
                StatusText.Text = $"{_updates.Count} update{(_updates.Count == 1 ? "" : "s")} available.";
                InstallButton.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Couldn't check for updates: {ex.Message}";
        }

        SetBusy(false);
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _updates.Count == 0) return;

        // Installing updates requires admin — elevate and reopen here if needed.
        if (!ElevationService.IsAdministrator())
        {
            if (ElevationService.RestartAsAdmin("--windowsupdate"))
                Application.Current.Shutdown(0);
            else
                StatusText.Text = "Administrator rights are required to install updates.";
            return;
        }

        SetBusy(true);
        InstallButton.Visibility = Visibility.Collapsed;
        var progress = new Progress<string>(s => StatusText.Text = s);
        var (ok, reboot, error) = await WindowsUpdateService.InstallAsync(_updates, progress);
        SetBusy(false);

        if (!ok)
        {
            StatusText.Text = $"Update failed: {error}";
            InstallButton.Visibility = Visibility.Visible;
            return;
        }

        UpdateList.ItemsSource = null;
        _updates = [];

        if (reboot || WindowsUpdateService.IsRebootPending())
        {
            WindowsUpdateService.ArmAutoOpen();
            StatusText.Text = "Updates installed. A restart is required to finish. " +
                              "Windows Tools will reopen here after you restart.";
            RestartButton.Visibility = Visibility.Visible;
        }
        else
        {
            StatusText.Text = "Updates installed successfully.";
        }
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        WindowsUpdateService.ArmAutoOpen();
        WindowsUpdateService.RestartNow();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        ScanButton.IsEnabled = !busy;
        ScanButton.Opacity = busy ? 0.5 : 1.0;
    }
}
