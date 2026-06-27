using System.Windows;
using System.Windows.Controls;
using WindowsTools.Services;
using WindowsTools.ViewModels;

namespace WindowsTools.Views;

public partial class DriverHubView : UserControl
{
    private readonly DriverHubViewModel _vm;
    private readonly SettingsService _settings;
    private bool _autoRan;

    public DriverHubView(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
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
    /// Automatically installs detected apps. Each install runs normally and
    /// shows the standard Windows (UAC) prompt when the package needs it.
    /// </summary>
    private async Task TryAutoInstallAsync()
    {
        if (!_settings.AutoInstallDrivers || !_vm.HasAppsToInstall) return;
        await _vm.AutoInstallAllAsync();
    }
}
