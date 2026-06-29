using System.Windows;
using System.Windows.Controls;
using WindowsTools.Services;
using WindowsTools.ViewModels;

namespace WindowsTools.Views;

public partial class DriverHubView : UserControl
{
    private readonly DriverHubViewModel _vm;
    private readonly SettingsService _settings;
    private bool _ran;

    public DriverHubView(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
        var detection = new HardwareDetectionService();
        var install = new AppInstallService(settings);
        _vm = new DriverHubViewModel(detection, install, settings);

        // White (not black) while the page loads.
        try { WebView.DefaultBackgroundColor = System.Drawing.Color.White; } catch { }

        Loaded += OnLoaded;
        Unloaded += (_, _) => { try { WebView.Dispose(); } catch { } };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_ran) return;
        _ran = true;

        // Navigate instantly to the last known driver URL — no waiting on detection.
        var cached = _settings.LastDriverEmbedUrl;
        if (!string.IsNullOrEmpty(cached))
            WebView.Source = new Uri(cached);

        // Refresh detection in the background and update the cache for next time.
        await _vm.ScanAsync();
        var url = _vm.RecommendedApps
            .Select(a => a.App.EmbedUrl)
            .FirstOrDefault(u => !string.IsNullOrEmpty(u));

        if (url is not null && url != cached)
        {
            _settings.LastDriverEmbedUrl = url;
            if (string.IsNullOrEmpty(cached)) // first run: navigate now
                WebView.Source = new Uri(url);
        }
    }
}
