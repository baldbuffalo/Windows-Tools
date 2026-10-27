using System.Diagnostics;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WindowsTools.Models;
using WindowsTools.Services;

namespace WindowsTools;

public sealed partial class MainWindow : Window
{
    private readonly SettingsService _settings = new();
    private readonly HardwareDetectionService _hardware = new();
    private readonly AppInstallService _appInstaller;
    private readonly Grid _shell = new();
    private readonly ContentControl _content = new();
    private readonly TextBlock _heading = new();
    private readonly Brush _surface = new SolidColorBrush(ColorHelper.FromArgb(255, 38, 38, 38));
    private readonly Brush _muted = new SolidColorBrush(ColorHelper.FromArgb(255, 180, 180, 180));
    private bool _startupHandled;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Windows Tools";
        _appInstaller = new AppInstallService(_settings);
        BuildShell();
        ShowStorage();
        Activated += HandleActivated;
    }

    private async void HandleActivated(object sender, WindowActivatedEventArgs args)
    {
        if (_startupHandled) return;
        _startupHandled = true;

        if (!InstallerService.IsRunningInstalled())
        {
            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = "Install Windows Tools?",
                Content = "Install to Program Files and create desktop and Start menu shortcuts, or continue running from this location.",
                PrimaryButtonText = "Install",
                CloseButtonText = "Run without installing",
                DefaultButton = ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath!,
                        Arguments = "--install-elevated",
                        UseShellExecute = true,
                        Verb = "runas"
                    });
                    Close();
                    return;
                }
                catch (Exception ex)
                {
                    await ShowDialogAsync("Installation not started", ex.Message);
                }
            }
        }

        if (_settings.CheckUpdatesOnStartup)
            _ = CheckUpdatesAsync(false);
    }

    private void BuildShell()
    {
        RootGrid.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 24, 24, 24));
        _shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(238) });
        _shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sidebar = new StackPanel { Spacing = 8, Padding = new Thickness(16, 22, 16, 16), Background = _surface };
        sidebar.Children.Add(new TextBlock { Text = "🛠  Windows Tools", FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(4, 0, 4, 2) });
        sidebar.Children.Add(new TextBlock { Text = "System utilities", Foreground = _muted, FontSize = 13, Margin = new Thickness(4, 0, 4, 20) });
        AddNav(sidebar, "💾  Storage", ShowStorage);
        AddNav(sidebar, "🔧  Driver Hub", ShowDriverHub);
        AddNav(sidebar, "🔃  Windows Update", ShowWindowsUpdate);
        AddNav(sidebar, "⚙  Settings", ShowSettings);
        sidebar.Children.Add(new TextBlock { Text = "v1.0.0 · WinUI 3", Foreground = _muted, FontSize = 12, Margin = new Thickness(4, 22, 0, 0) });

        var main = new Grid { Padding = new Thickness(28, 24, 28, 24) };
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _heading.FontSize = 28;
        _heading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        _heading.Margin = new Thickness(0, 0, 0, 20);
        main.Children.Add(_heading);
        Grid.SetRow(_content, 1);
        main.Children.Add(_content);
        Grid.SetColumn(sidebar, 0);
        Grid.SetColumn(main, 1);
        _shell.Children.Add(sidebar);
        _shell.Children.Add(main);
        RootGrid.Children.Add(_shell);
    }

    private void AddNav(Panel panel, string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(14, 11, 14, 11),
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 50, 50, 50))
        };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
    }

    private StackPanel Page(string title, string description)
    {
        _heading.Text = title;
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock { Text = description, Foreground = _muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, -12, 0, 8) });
        _content.Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        return stack;
    }

    private void ShowStorage()
    {
        var page = Page("Storage", "Inspect the capacity and usage of connected drives.");
        var refresh = new Button { Content = "Refresh drives", HorizontalAlignment = HorizontalAlignment.Left };
        refresh.Click += (_, _) => ShowStorage();
        page.Children.Add(refresh);

        var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
        foreach (var drive in drives)
        {
            var used = Math.Max(0, drive.TotalSize - drive.TotalFreeSpace);
            var percent = drive.TotalSize > 0 ? Math.Clamp((double)used / drive.TotalSize * 100, 0, 100) : 0;
            var card = Card();
            card.Children.Add(new TextBlock { Text = string.Format("{0} · {1}", drive.Name, SafeLabel(drive)), FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            card.Children.Add(new TextBlock { Text = string.Format("{0} used of {1} · {2} free", Bytes(used), Bytes(drive.TotalSize), Bytes(drive.TotalFreeSpace)), Foreground = _muted });
            card.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = percent, Height = 8 });
            card.Children.Add(new TextBlock { Text = string.Format("{0:F1}% used · {1} · {2}", percent, drive.DriveType, drive.DriveFormat), Foreground = _muted, FontSize = 12 });
            page.Children.Add(card);
        }

        if (drives.Count == 0)
            page.Children.Add(new TextBlock { Text = "No ready drives were found.", Foreground = _muted });
    }

    private async void ShowDriverHub()
    {
        var page = Page("Driver Hub", "Detect hardware and find matching manufacturer support tools.");
        var status = new TextBlock { Text = "Scanning hardware…", Foreground = _muted };
        page.Children.Add(status);
        var refresh = new Button { Content = "Rescan hardware", HorizontalAlignment = HorizontalAlignment.Left };
        refresh.Click += (_, _) => ShowDriverHub();
        page.Children.Add(refresh);

        var detection = await Task.Run(() => _hardware.Detect());
        status.Text = "Hardware scan complete.";
        foreach (var hardware in detection.hardware)
        {
            var card = Card();
            card.Children.Add(new TextBlock { Text = hardware.Icon + "  " + hardware.Category, Foreground = _muted, FontSize = 12 });
            card.Children.Add(new TextBlock { Text = hardware.Name, FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            if (!string.IsNullOrWhiteSpace(hardware.Detail))
                card.Children.Add(new TextBlock { Text = hardware.Detail, Foreground = _muted });
            page.Children.Add(card);
        }

        foreach (var app in detection.apps)
        {
            var card = Card();
            card.Children.Add(new TextBlock { Text = app.Icon + "  " + app.Name, FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            card.Children.Add(new TextBlock { Text = app.Description, Foreground = _muted, TextWrapping = TextWrapping.Wrap });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var install = new Button { Content = "Install with winget" };
            var website = new Button { Content = "Manufacturer website" };
            var resultText = new TextBlock { Foreground = _muted, TextWrapping = TextWrapping.Wrap };
            install.Click += async (_, _) =>
            {
                install.IsEnabled = false;
                var progress = new Progress<string>(message => resultText.Text = message);
                var result = await _appInstaller.InstallAsync(app, progress, CancellationToken.None);
                resultText.Text = result.success ? "Installation complete." : result.error ?? "Installation did not complete.";
                install.IsEnabled = true;
            };
            website.Click += (_, _) => OpenUrl(app.DownloadPageUrl);
            buttons.Children.Add(install);
            buttons.Children.Add(website);
            card.Children.Add(buttons);
            card.Children.Add(resultText);
            page.Children.Add(card);
        }
    }

    private void ShowWindowsUpdate()
    {
        var page = Page("Windows Update", "Windows manages system updates in Settings.");
        page.Children.Add(new TextBlock { Text = "Open Windows Update to check for Windows, driver, and security updates.", TextWrapping = TextWrapping.Wrap });
        var button = new Button { Content = "Open Windows Update", HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => OpenUrl("ms-settings:windowsupdate");
        page.Children.Add(button);
    }

    private void ShowSettings()
    {
        var page = Page("Settings", "Configure Windows Tools and check for application updates.");
        var startup = new CheckBox { Content = "Check for updates on startup", IsChecked = _settings.CheckUpdatesOnStartup };
        startup.Checked += (_, _) => _settings.CheckUpdatesOnStartup = true;
        startup.Unchecked += (_, _) => _settings.CheckUpdatesOnStartup = false;
        var drivers = new CheckBox { Content = "Automatically install recommended driver tools", IsChecked = _settings.AutoInstallDrivers };
        drivers.Checked += (_, _) => _settings.AutoInstallDrivers = true;
        drivers.Unchecked += (_, _) => _settings.AutoInstallDrivers = false;
        page.Children.Add(startup);
        page.Children.Add(drivers);
        page.Children.Add(new TextBlock { Text = "Install location: " + InstallerService.InstallExePath, Foreground = _muted, TextWrapping = TextWrapping.Wrap });
        var status = new TextBlock { Text = "Check the latest GitHub release for an update.", Foreground = _muted, TextWrapping = TextWrapping.Wrap };
        var check = new Button { Content = "Check for updates", HorizontalAlignment = HorizontalAlignment.Left };
        check.Click += async (_, _) =>
        {
            check.IsEnabled = false;
            status.Text = "Checking latest release…";
            var info = await UpdateService.CheckAsync();
            status.Text = info.Error is not null ? "Couldn't check: " + info.Error :
                info.UpdateAvailable ? "A newer version is available." : "You're on the latest version.";
            if (info.UpdateAvailable)
            {
                var release = new Button { Content = "Open latest release", HorizontalAlignment = HorizontalAlignment.Left };
                release.Click += (_, _) => OpenUrl("https://github.com/baldbuffalo/Windows-Tools/releases/tag/latest");
                page.Children.Add(release);
            }
            check.IsEnabled = true;
        };
        page.Children.Add(check);
        page.Children.Add(status);
    }

    private async Task CheckUpdatesAsync(bool showResult)
    {
        var info = await UpdateService.CheckAsync();
        if (!info.UpdateAvailable || !showResult) return;
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "Update available",
            Content = "A newer version of Windows Tools is available.",
            PrimaryButtonText = "Open release",
            CloseButtonText = "Later"
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            OpenUrl("https://github.com/baldbuffalo/Windows-Tools/releases/tag/latest");
    }

    private StackPanel Card() => new()
    {
        Spacing = 7,
        Padding = new Thickness(16),
        Margin = new Thickness(0, 0, 0, 4),
        Background = _surface
    };

    private static string SafeLabel(DriveInfo drive)
    {
        try { return string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Local drive" : drive.VolumeLabel; }
        catch { return "Drive"; }
    }

    private static string Bytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return string.Format("{0:0.##} {1}", value, units[unit]);
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    private async Task ShowDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog { XamlRoot = RootGrid.XamlRoot, Title = title, Content = message, CloseButtonText = "OK" };
        await dialog.ShowAsync();
    }
}
