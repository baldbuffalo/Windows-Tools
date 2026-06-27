using System.Windows;
using System.Windows.Controls;
using WindowsTools.Services;
using WindowsTools.ViewModels;

namespace WindowsTools.Views;

public partial class DriverHubView : UserControl
{
    private readonly DriverHubViewModel _vm;
    private bool _autoRan;

    public DriverHubView(SettingsService settings)
    {
        InitializeComponent();
        var detection = new HardwareDetectionService();
        var install = new AppInstallService(settings);
        _vm = new DriverHubViewModel(detection, install, settings);
        DataContext = _vm;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_autoRan) return;
        _autoRan = true;
        await _vm.ScanAsync();
        await TryAutoInstallAsync();
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        await _vm.ScanAsync();
        await TryAutoInstallAsync();
    }

    /// <summary>
    /// Automatically installs detected apps. If we aren't elevated, relaunch as
    /// admin first so winget can install silently (no per-app UAC prompt).
    /// </summary>
    private async Task TryAutoInstallAsync()
    {
        if (!_vm.HasAppsToInstall) return;

        if (!ElevationService.IsAdministrator())
        {
            // Relaunch elevated and re-open Driver Hub to finish there.
            if (ElevationService.RestartAsAdmin("--driverhub"))
            {
                Application.Current.Shutdown(0);
                return;
            }
            // User declined elevation — leave the manual Install buttons.
            return;
        }

        await _vm.AutoInstallAllAsync();
    }
}
