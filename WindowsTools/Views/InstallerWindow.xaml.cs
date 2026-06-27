using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using WindowsTools.Models;
using WindowsTools.Services;

namespace WindowsTools.Views;

public partial class InstallerWindow : Window
{
    private readonly ObservableCollection<InstallStep> _steps =
    [
        new() { Title = "Install application" },
        new() { Title = "Detect your hardware" },
        new() { Title = "Driver updater app" },
        new() { Title = "Finished" },
    ];

    private readonly SettingsService _settings = new();

    public InstallerWindow()
    {
        InitializeComponent();
        StepsList.ItemsSource = _steps;
        Progress.ValueChanged += (_, e) => PercentText.Text = $"{(int)e.NewValue}%";
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Step 1 — install the app + desktop shortcut.
        Begin(0, "Installing application", "Copying Windows Tools and creating a desktop shortcut...");
        var copied = await Task.Run(InstallerService.CopyExe);
        await Task.Run(InstallerService.CreateShortcuts);
        await AnimateTo(25, TimeSpan.FromMilliseconds(700));
        if (!copied)
        {
            // Couldn't install — just run from here.
            StatusText.Text = "Couldn't install — starting in place...";
            await Task.Delay(900);
            new MainWindow().Show();
            Close();
            return;
        }
        Done(0);

        // Step 2 — detect hardware / manufacturer.
        Begin(1, "Detecting your hardware", "Checking your CPU, GPU and PC manufacturer...");
        var detection = new HardwareDetectionService();
        var (hardware, apps) = await Task.Run(detection.Detect);
        var mfr = string.Join(", ", hardware.Select(h => h.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
        StatusText.Text = string.IsNullOrWhiteSpace(mfr)
            ? "Hardware detected."
            : $"Detected:\n{string.Join("\n", hardware.Select(h => $"{h.Icon}  {h.Name}"))}";
        await AnimateTo(50, TimeSpan.FromMilliseconds(700));
        Done(1);

        // Step 3 — install the matching driver updater app(s), normally (with UAC).
        Begin(2, "Installing driver updater app",
            "Installing the app for your hardware. Approve the Windows prompt if it appears...");
        if (apps.Count == 0)
        {
            StatusText.Text = "No manufacturer driver app is needed for this system.";
            await AnimateTo(90, TimeSpan.FromMilliseconds(600));
        }
        else
        {
            var install = new AppInstallService(_settings);
            var step = 40.0 / apps.Count;
            var basePct = 50.0;
            foreach (var app in apps)
            {
                var progress = new Progress<string>(s =>
                    StatusText.Text = $"{app.Name}\n{s}\n\n(Approve the Windows prompt if it appears.)");
                await install.InstallAsync(app, progress, CancellationToken.None);
                basePct += step;
                await AnimateTo(basePct, TimeSpan.FromMilliseconds(300));
            }
        }
        Done(2);

        // Step 4 — finished.
        Begin(3, "Setup complete", "Windows Tools is installed. A shortcut was added to your desktop.");
        await AnimateTo(100, TimeSpan.FromMilliseconds(500));
        Done(3);
        FinishButton.Visibility = Visibility.Visible;
    }

    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        InstallerService.LaunchInstalled();
        Application.Current.Shutdown(0);
    }

    private void Begin(int index, string title, string status)
    {
        for (var i = 0; i < _steps.Count; i++)
            if (i < index) _steps[i].State = StepState.Done;
        _steps[index].State = StepState.Active;
        StepTitle.Text = title;
        StatusText.Text = status;
    }

    private void Done(int index) => _steps[index].State = StepState.Done;

    private Task AnimateTo(double target, TimeSpan duration)
    {
        var tcs = new TaskCompletionSource();
        var anim = new DoubleAnimation(target, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        anim.Completed += (_, _) => tcs.TrySetResult();
        Progress.BeginAnimation(RangeBase.ValueProperty, anim);
        return tcs.Task;
    }
}
