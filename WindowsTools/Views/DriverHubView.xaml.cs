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
    private bool _ran;

    public DriverHubView(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
        var detection = new HardwareDetectionService();
        var install = new AppInstallService(settings);
        _vm = new DriverHubViewModel(detection, install, settings);

        // Run the WebView with GPU acceleration OFF. Updating the graphics driver
        // resets the GPU, which would otherwise crash the WebView's GPU process
        // (and take the whole app down). Software rendering is immune to that.
        WebView.CreationProperties = new CoreWebView2CreationProperties
        {
            AdditionalBrowserArguments = "--disable-gpu --disable-gpu-compositing"
        };

        try { WebView.DefaultBackgroundColor = System.Drawing.Color.White; } catch { }

        // If the web content process still fails for any reason, reload instead of dying.
        WebView.CoreWebView2InitializationCompleted += (_, e) =>
        {
            if (!e.IsSuccess) return;
            WebView.CoreWebView2.ProcessFailed += (_, _) =>
            {
                try { WebView.Reload(); } catch { }
            };
        };

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
