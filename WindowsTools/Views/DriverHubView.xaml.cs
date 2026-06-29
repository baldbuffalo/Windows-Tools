using System.Windows;
using System.Windows.Controls;
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
        Unloaded += (_, _) => { try { WebView.Dispose(); } catch { } };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_autoRan) return;
        _autoRan = true;

        await _vm.ScanAsync();

        // Show the installed app's web UI as soon as detection resolves.
        var url = _vm.RecommendedApps
            .Select(a => a.App.EmbedUrl)
            .FirstOrDefault(u => !string.IsNullOrEmpty(u));
        if (url is not null)
            WebView.Source = new Uri(url);
        else
            EmbedHost.Visibility = Visibility.Collapsed; // no embeddable app — show the list

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

    private void OpenApp_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not AppViewModel vm) return;
        if (string.IsNullOrEmpty(vm.App.EmbedUrl)) return;
        WebView.Source = new Uri(vm.App.EmbedUrl);
        EmbedHost.Visibility = Visibility.Visible;
    }
}
