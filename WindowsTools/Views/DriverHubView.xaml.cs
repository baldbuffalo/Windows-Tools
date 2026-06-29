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

        // Web-based tools (e.g. Intel DSA) embed their page in a WebView2.
        if (!string.IsNullOrEmpty(app.EmbedUrl))
        {
            EmbedTitle.Text = app.Name;
            EmbedHost.Child = new Microsoft.Web.WebView2.Wpf.WebView2 { Source = new Uri(app.EmbedUrl) };
            EmbedPanel.Visibility = Visibility.Visible;
            return;
        }

        // Normal Win32 apps embed via window reparenting.
        if (_install.CanEmbed(app))
        {
            EmbedTitle.Text = app.Name;
            EmbedHost.Child = new EmbeddedAppHost(() => _install.StartAppProcess(app));
            EmbedPanel.Visibility = Visibility.Visible;
            return;
        }

        // Store apps can't be embedded — launch them normally.
        _install.LaunchApp(app);
    }

    private void EmbedBack_Click(object sender, RoutedEventArgs e)
    {
        switch (EmbedHost.Child)
        {
            case EmbeddedAppHost host: host.Dispose(); break;
            case Microsoft.Web.WebView2.Wpf.WebView2 web: web.Dispose(); break;
        }
        EmbedHost.Child = null;
        EmbedPanel.Visibility = Visibility.Collapsed;
    }
}
