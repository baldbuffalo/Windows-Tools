using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsTools.Services;

namespace WindowsTools.Views;

public partial class WindowsUpdateView : UserControl
{
    private enum State { Checking, UpToDate, Available, Installing, RestartRequired }

    private static readonly Brush Blue = (Brush)new BrushConverter().ConvertFromString("#FF0078D4")!;
    private static readonly Brush Green = (Brush)new BrushConverter().ConvertFromString("#FF4CAF50")!;
    private static readonly Brush Amber = (Brush)new BrushConverter().ConvertFromString("#FFFFB900")!;

    private List<WindowsUpdateItem> _updates = [];
    private State _state;
    private bool _ran;

    public WindowsUpdateView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_ran) return;
        _ran = true;

        if (WindowsUpdateService.IsRebootPending())
        {
            WindowsUpdateService.ArmAutoOpen();
            SetState(State.RestartRequired);
            return;
        }

        WindowsUpdateService.DisarmAutoOpen();
        await ScanAsync();
    }

    private async Task ScanAsync()
    {
        SetState(State.Checking);
        try
        {
            _updates = await WindowsUpdateService.ScanAsync();
            UpdateList.ItemsSource = _updates;
            SetState(_updates.Count > 0 ? State.Available : State.UpToDate);
        }
        catch (Exception ex)
        {
            SetState(State.UpToDate);
            HeadingText.Text = "Couldn't check for updates";
            SubText.Text = ex.Message;
        }
    }

    private async Task InstallAsync()
    {
        // Installing requires admin — elevate and reopen here if needed.
        if (!ElevationService.IsAdministrator())
        {
            if (ElevationService.RestartAsAdmin("--windowsupdate"))
                Application.Current.Shutdown(0);
            else
                SubText.Text = "Administrator rights are required to install updates.";
            return;
        }

        SetState(State.Installing);
        var progress = new Progress<string>(s => SubText.Text = s);
        var (ok, reboot, error) = await WindowsUpdateService.InstallAsync(_updates, progress);

        if (!ok)
        {
            SetState(State.Available);
            SubText.Text = $"Update failed: {error}";
            return;
        }

        _updates = [];
        UpdateList.ItemsSource = null;

        if (reboot || WindowsUpdateService.IsRebootPending())
        {
            WindowsUpdateService.ArmAutoOpen();
            SetState(State.RestartRequired);
        }
        else
        {
            SetState(State.UpToDate);
            HeadingText.Text = "Updates installed";
        }
    }

    private async void Action_Click(object sender, RoutedEventArgs e)
    {
        switch (_state)
        {
            case State.UpToDate: await ScanAsync(); break;
            case State.Available: await InstallAsync(); break;
            case State.RestartRequired:
                WindowsUpdateService.ArmAutoOpen();
                WindowsUpdateService.RestartNow();
                break;
        }
    }

    private void SetState(State state)
    {
        _state = state;
        ListHeader.Visibility = state == State.Available ? Visibility.Visible : Visibility.Collapsed;

        switch (state)
        {
            case State.Checking:
                StatusGlyph.Text = "↻"; StatusIcon.Background = Blue;
                HeadingText.Text = "Checking for updates...";
                SubText.Text = "";
                ActionButton.Visibility = Visibility.Collapsed;
                break;

            case State.UpToDate:
                StatusGlyph.Text = "✓"; StatusIcon.Background = Green;
                HeadingText.Text = "You're up to date";
                SubText.Text = $"Last checked: today, {DateTime.Now:t}";
                ActionButton.Content = "Check for updates";
                ActionButton.Visibility = Visibility.Visible;
                break;

            case State.Available:
                StatusGlyph.Text = "⬇"; StatusIcon.Background = Blue;
                HeadingText.Text = $"{_updates.Count} update{(_updates.Count == 1 ? "" : "s")} available";
                SubText.Text = "Updates are ready to download and install.";
                ActionButton.Content = "Download & install";
                ActionButton.Visibility = Visibility.Visible;
                break;

            case State.Installing:
                StatusGlyph.Text = "⬇"; StatusIcon.Background = Blue;
                HeadingText.Text = "Installing updates...";
                SubText.Text = "Downloading updates...";
                ActionButton.Visibility = Visibility.Collapsed;
                break;

            case State.RestartRequired:
                StatusGlyph.Text = "↻"; StatusIcon.Background = Amber;
                HeadingText.Text = "Restart required";
                SubText.Text = "A restart is required to finish installing updates. " +
                               "Windows Tools will reopen here after you restart.";
                ActionButton.Content = "Restart now";
                ActionButton.Visibility = Visibility.Visible;
                break;
        }
    }
}
