using System.Windows.Controls;
using WindowsTools.Services;
using WindowsTools.ViewModels;

namespace WindowsTools.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsService settings)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(settings);
    }
}
