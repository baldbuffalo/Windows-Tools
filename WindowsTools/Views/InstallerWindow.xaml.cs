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
    private int _current = -1;
    private bool _busy;
    private List<ManufacturerApp> _apps = [];

    public InstallerWindow()
    {
        InitializeComponent();
        StepsList.ItemsSource = _steps;
        Progress.ValueChanged += (_, e) => PercentText.Text = $"{(int)e.NewValue}%";
        Loaded += async (_, _) => await GoToStep(0);
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_current >= _steps.Count - 1)
        {
            InstallerService.LaunchInstalled();
            Application.Current.Shutdown(0);
            return;
        }
        await GoToStep(_current + 1);
    }

    private async Task GoToStep(int index)
    {
        _current = index;
        for (var i = 0; i < _steps.Count; i++)
            _steps[i].State = i < index ? StepState.Done : i == index ? StepState.Active : StepState.Pending;

        SetBusy(true);
        NextButton.Content = index >= _steps.Count - 1 ? "Finish" : "Next";

        switch (index)
        {
            case 0: await DoInstallStep(); break;
            case 1: await DoDetectStep(); break;
            case 2: await DoDriverStep(); break;
            case 3: DoFinishStep(); break;
        }

        _steps[index].State = StepState.Done;
        if (index == _steps.Count - 1) _steps[index].State = StepState.Active;
        SetBusy(false);
    }

    private async Task DoInstallStep()
    {
        StepTitle.Text = "Installing application";
        StatusText.Text = "Copying Windows Tools and creating a desktop shortcut...";
        var copied = await Task.Run(InstallerService.CopyExe);
        await Task.Run(InstallerService.CreateShortcuts);
        await AnimateTo(100, TimeSpan.FromMilliseconds(600));
        StatusText.Text = copied
            ? "Windows Tools was installed and a desktop shortcut was created."
            : "Couldn't copy to the install folder — the app will run in place.";
    }

    private async Task DoDetectStep()
    {
        StepTitle.Text = "Detecting your hardware";
        StatusText.Text = "Checking your CPU, GPU and PC manufacturer...";
        Progress.BeginAnimation(RangeBase.ValueProperty, null);
        Progress.Value = 0;

        var detection = new HardwareDetectionService();
        var (hardware, apps) = await Task.Run(detection.Detect);
        _apps = apps;

        await AnimateTo(100, TimeSpan.FromMilliseconds(600));
        StatusText.Text = hardware.Count == 0
            ? "Hardware detected."
            : "Detected:\n" + string.Join("\n", hardware.Select(h => $"{h.Category}:  {h.Name}"));
    }

    private async Task DoDriverStep()
    {
        StepTitle.Text = "Installing driver updater app";
        Progress.BeginAnimation(RangeBase.ValueProperty, null);
        Progress.Value = 0;

        if (_apps.Count == 0)
        {
            StatusText.Text = "No manufacturer driver app is needed for this system.";
            await AnimateTo(100, TimeSpan.FromMilliseconds(500));
            return;
        }

        var install = new AppInstallService(_settings);
        var step = 100.0 / _apps.Count;
        var pct = 0.0;
        var anyInstalled = false;
        foreach (var app in _apps)
        {
            if (install.IsAppInstalled(app))
            {
                StatusText.Text = $"{app.Name} is already installed on this PC. Skipping.";
                await Task.Delay(700);
            }
            else
            {
                var progress = new Progress<string>(s =>
                    StatusText.Text = $"{app.Name}\n{s}\n\n(Approve the Windows prompt if it appears.)");
                await install.InstallAsync(app, progress, CancellationToken.None);
                anyInstalled = true;
            }
            pct += step;
            await AnimateTo(pct, TimeSpan.FromMilliseconds(300));
        }
        StatusText.Text = anyInstalled
            ? "Driver updater app installed."
            : "Your driver updater app is already installed.";
    }

    private void DoFinishStep()
    {
        StepTitle.Text = "Setup complete";
        StatusText.Text = "Windows Tools is ready. Click Finish to open it.";
        Progress.BeginAnimation(RangeBase.ValueProperty, null);
        Progress.Value = 100;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        NextButton.IsEnabled = !busy;
        NextButton.Opacity = busy ? 0.5 : 1.0;
    }

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
