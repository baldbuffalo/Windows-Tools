using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;
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

    // Auto-show the embeddable web tool (e.g. Intel DSA) full-screen, no clicks.
    private void TryAutoEmbed()
    {
        var app = _vm.RecommendedApps
            .Select(a => a.App)
            .FirstOrDefault(a => !string.IsNullOrEmpty(a.EmbedUrl));
        if (app is null) return;
        ShowEmbed(new WebView2 { Source = new Uri(app.EmbedUrl!) });
    }

    private void OpenApp_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not AppViewModel vm) return;
        var app = vm.App;

        if (!string.IsNullOrEmpty(app.EmbedUrl))
            ShowEmbed(new WebView2 { Source = new Uri(app.EmbedUrl) });
        else if (_install.CanEmbed(app))
            ShowEmbed(new EmbeddedAppHost(() => _install.StartAppProcess(app)));
        else
            _install.LaunchApp(app);
    }

    private void ShowEmbed(UIElement child)
    {
        DisposeEmbed();
        EmbedHost.Child = child;
        EmbedHost.Visibility = Visibility.Visible;
    }

    private void DisposeEmbed()
    {
        switch (EmbedHost.Child)
        {
            case EmbeddedAppHost host: host.Dispose(); break;
            case WebView2 web: web.Dispose(); break;
        }
        EmbedHost.Child = null;
        EmbedHost.Visibility = Visibility.Collapsed;
    }
}
