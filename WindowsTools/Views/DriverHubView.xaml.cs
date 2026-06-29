using System.Windows;
using System.Windows.Controls;
using WindowsTools.Services;
using WindowsTools.ViewModels;

namespace WindowsTools.Views;

public partial class DriverHubView : UserControl
{
    private readonly DriverHubViewModel _vm;
    private bool _ran;

    public DriverHubView(SettingsService settings)
    {
        InitializeComponent();
        var detection = new HardwareDetectionService();
        var install = new AppInstallService(settings);
        _vm = new DriverHubViewModel(detection, install, settings);
        Loaded += OnLoaded;
        Unloaded += (_, _) => { try { WebView.Dispose(); } catch { } };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_ran) return;
        _ran = true;

        await _vm.ScanAsync();

        var url = _vm.RecommendedApps
            .Select(a => a.App.EmbedUrl)
            .FirstOrDefault(u => !string.IsNullOrEmpty(u));

        if (url is not null)
            WebView.Source = new Uri(url);
    }
}
