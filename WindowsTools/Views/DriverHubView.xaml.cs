using System.Windows;
using System.Windows.Controls;
using WindowsTools.Controls;
using WindowsTools.Services;
using WindowsTools.ViewModels;

namespace WindowsTools.Views;

public partial class DriverHubView : UserControl
{
    private readonly DriverHubViewModel _vm;
    private readonly SettingsService _settings;
    private readonly AppInstallService _install;
    private bool _autoRan;

    public DriverHubView(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
        var detection = new HardwareDetectionService();
        _install = new AppInstallService(settings);
        _vm = new DriverHubViewModel(detection, _install, settings);
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

    private async Task TryAutoInstallAsync()
    {
        if (!_settings.AutoInstallDrivers || !_vm.HasAppsToInstall) return;
        await _vm.AutoInstallAllAsync();
    }

    // Opens an installed app embedded inside the Driver Hub (no external window).
    private void OpenApp_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not AppViewModel vm) return;
        var app = vm.App;

        // Store apps can't be reparented — launch them normally.
        if (!_install.CanEmbed(app))
        {
            _install.LaunchApp(app);
            return;
        }

        EmbedTitle.Text = app.Name;
        EmbedHost.Child = new EmbeddedAppHost(() => _install.StartAppProcess(app));
        EmbedPanel.Visibility = Visibility.Visible;
    }

    private void EmbedBack_Click(object sender, RoutedEventArgs e)
    {
        if (EmbedHost.Child is EmbeddedAppHost host) host.Dispose();
        EmbedHost.Child = null;
        EmbedPanel.Visibility = Visibility.Collapsed;
    }
}
