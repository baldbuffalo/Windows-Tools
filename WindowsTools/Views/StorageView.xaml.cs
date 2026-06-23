using System.Windows.Controls;
using WindowsTools.ViewModels;

namespace WindowsTools.Views;

public partial class StorageView : UserControl
{
    private readonly StorageViewModel _vm = new();

    public StorageView()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.Refresh();
    }

    private void RefreshButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _vm.Refresh();
    }
}
