using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using WindowsTools.Services;

namespace WindowsTools.Views;

public partial class InstallerWindow : Window
{
    public InstallerWindow()
    {
        InitializeComponent();
        Progress.ValueChanged += (_, e) => PercentText.Text = $"{(int)e.NewValue}%";
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Step 1 — copy the exe into the install folder.
        StatusText.Text = "Installing Windows Tools...";
        var copyTask = Task.Run(InstallerService.CopyExe);
        await AnimateTo(55, TimeSpan.FromMilliseconds(900));
        var copied = await copyTask;

        if (!copied)
        {
            // Couldn't install — just run the app from where it is.
            StatusText.Text = "Couldn't install — starting in place...";
            await Task.Delay(900);
            new MainWindow().Show();
            Close();
            return;
        }

        // Step 2 — create the desktop (and Start Menu) shortcut.
        StatusText.Text = "Creating desktop shortcut...";
        var shortcutTask = Task.Run(InstallerService.CreateShortcuts);
        await AnimateTo(100, TimeSpan.FromMilliseconds(800));
        await shortcutTask;

        // Done — hand off to the installed copy.
        StatusText.Text = "Done! Opening Windows Tools...";
        await Task.Delay(700);

        InstallerService.LaunchInstalled();
        Application.Current.Shutdown(0);
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
