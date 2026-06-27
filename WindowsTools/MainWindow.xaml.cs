using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowsTools.Services;
using WindowsTools.Views;

namespace WindowsTools;

public partial class MainWindow : Window
{
    private readonly SettingsService _settings = new();
    private Button? _activeNavButton;

    public MainWindow()
    {
        InitializeComponent();
        InstalledAppsList.ItemsSource = _settings.InstalledApps;

        // When relaunched elevated to finish driver installs, open Driver Hub.
        // Otherwise PageContent keeps its XAML default (StorageView).
        if (Environment.GetCommandLineArgs().Contains("--driverhub"))
        {
            SetActive(DriverHubNavButton);
            PageContent.Content = new DriverHubView(_settings);
        }
        else
        {
            SetActive(StorageNavButton);
        }
    }

    private void SetActive(Button btn)
    {
        if (_activeNavButton != null)
            _activeNavButton.Style = (Style)FindResource("NavButtonStyle");
        btn.Style = (Style)FindResource("NavButtonActiveStyle");
        _activeNavButton = btn;
    }

    private void StorageNavButton_Click(object sender, RoutedEventArgs e)
    {
        SetActive(StorageNavButton);
        PageContent.Content = new StorageView();
    }

    private void DriverHubNavButton_Click(object sender, RoutedEventArgs e)
    {
        SetActive(DriverHubNavButton);
        PageContent.Content = new DriverHubView(_settings);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleMaximize();
        else DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
