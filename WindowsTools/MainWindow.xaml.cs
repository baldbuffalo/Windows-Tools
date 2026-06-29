using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using WindowsTools.Models;
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

        // Sidebar lists only OEM-suite apps. Driver updater apps live in Driver Hub.
        var apps = CollectionViewSource.GetDefaultView(_settings.InstalledApps);
        apps.Filter = o => o is InstalledAppEntry e && e.Category != AppCategory.DriverUpdater;
        InstalledAppsList.ItemsSource = apps;

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

        Loaded += async (_, _) => await MaybeCheckUpdatesAsync();
    }

    private async Task MaybeCheckUpdatesAsync()
    {
        if (!_settings.CheckUpdatesOnStartup) return;

        var info = await UpdateService.CheckAsync();
        if (!info.UpdateAvailable) return;

        var result = MessageBox.Show(
            "A new version of Windows Tools is available.\n\nOpen Settings to update now?",
            "Update available", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result == MessageBoxResult.Yes)
            SettingsNavButton_Click(this, new RoutedEventArgs());
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

    private void SettingsNavButton_Click(object sender, RoutedEventArgs e)
    {
        SetActive(SettingsNavButton);
        PageContent.Content = new SettingsView(_settings);
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
