using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;
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
        Unloaded += (_, _) => DisposeEmbed();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_autoRan) return;
        _autoRan = true;
        await _vm.ScanAsync();
        await TryAutoInstallAsync();
        TryAutoEmbed();
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        await _vm.ScanAsync();
        await TryAutoInstallAsync();
        TryAutoEmbed();
    }

    private async Task TryAutoInstallAsync()
    {
        if (!_settings.AutoInstallDrivers || !_vm.HasAppsToInstall) return;
        await _vm.AutoInstallAllAsync();
    }

    // Auto-show the manufacturer's web tool full-screen, no clicks.
    private void TryAutoEmbed()
    {
        var url = _vm.RecommendedApps
            .Select(a => a.App.EmbedUrl ?? a.App.DownloadPageUrl)
            .FirstOrDefault(u => !string.IsNullOrEmpty(u));
        if (url is not null) ShowEmbed(url);
    }

    private void OpenApp_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not AppViewModel vm) return;
        var url = vm.App.EmbedUrl ?? vm.App.DownloadPageUrl;
        if (!string.IsNullOrEmpty(url)) ShowEmbed(url);
    }

    private void ShowEmbed(string url)
    {
        DisposeEmbed();
        EmbedHost.Child = new WebView2 { Source = new Uri(url) };
        EmbedHost.Visibility = Visibility.Visible;
    }

    private void DisposeEmbed()
    {
        if (EmbedHost.Child is WebView2 web) web.Dispose();
        EmbedHost.Child = null;
        EmbedHost.Visibility = Visibility.Collapsed;
    }
}
