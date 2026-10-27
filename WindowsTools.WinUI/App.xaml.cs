using Microsoft.UI.Xaml;
using WindowsTools.Services;

namespace WindowsTools;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (Environment.GetCommandLineArgs().Contains("--install-elevated"))
        {
            try
            {
                if (InstallerService.Install() is not null)
                    InstallerService.LaunchInstalled();
            }
            catch { }
            Exit();
            return;
        }

        _window = new MainWindow();
        _window.Activate();
    }
}
