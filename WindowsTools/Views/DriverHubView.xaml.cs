using System.Windows.Controls;
using WindowsTools.Services;
using WindowsTools.ViewModels;

namespace WindowsTools.Views;

public partial class DriverHubView : UserControl
{
    private readonly DriverHubViewModel _vm;

    public DriverHubView(SettingsService settings)
    {
        InitializeComponent();
        var detection = new HardwareDetectionService();
        var install = new AppInstallService(settings);
        _vm = new DriverHubViewModel(detection, install, settings);
        DataContext = _vm;
    }

    private async void ScanButton_Click(object sender, System.Windows.RoutedEventArgs e)
        => await _vm.ScanAsync();
}
