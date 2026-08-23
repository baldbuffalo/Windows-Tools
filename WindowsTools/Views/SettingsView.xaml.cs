using System.Windows.Controls;
using WindowsTools.Services;
using WindowsTools.ViewModels;

namespace WindowsTools.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsService settings, WindowsSettingsService windowsSettings)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(settings, windowsSettings);
    }
}
