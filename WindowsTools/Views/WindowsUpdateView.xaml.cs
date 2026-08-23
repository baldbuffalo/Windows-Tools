using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WindowsTools.Views;

public partial class WindowsUpdateView : UserControl
{
    private static readonly Brush Blue = (Brush)new BrushConverter().ConvertFromString("#FF0078D4")!;
    private readonly Action _launchWindowsUpdate;
    private bool _launched;

    public WindowsUpdateView(Action launchWindowsUpdate)
    {
        InitializeComponent();
        _launchWindowsUpdate = launchWindowsUpdate;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_launched) return;
        _launched = true;

        // Windows Tools does not perform Windows Update itself. This page is
        // only a visual hand-off while the real Windows Update page launches.
        StatusIcon.Background = Blue;
        StatusGlyph.Text = "↗";
        HeadingText.Text = "Launching Windows Update...";
        SubText.Text = "Opening Windows Settings...";
        ActionButton.Visibility = Visibility.Collapsed;
        ListHeader.Visibility = Visibility.Collapsed;
        UpdateList.ItemsSource = null;

        await Task.Delay(250);
        _launchWindowsUpdate();
    }
}
