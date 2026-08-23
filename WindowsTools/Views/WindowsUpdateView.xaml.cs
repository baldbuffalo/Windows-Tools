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

        StatusIcon.Background = Blue;
        StatusGlyph.Text = "↗";
        HeadingText.Text = "Launching Windows Update...";
        SubText.Text = "Opening Windows Settings...";

        await Task.Delay(250);
        _launchWindowsUpdate();
    }
}
