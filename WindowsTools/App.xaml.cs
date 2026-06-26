using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using WindowsTools.Services;
using WindowsTools.Views;

namespace WindowsTools;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "WindowsTools-crash.log");

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, e) => Log(e.Exception, "UnobservedTask");
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // When launched from outside the install folder (e.g. Downloads),
        // show the installer UI which copies us in, adds a desktop shortcut,
        // and launches the installed copy. The window drives the rest.
        if (!InstallerService.IsRunningInstalled())
        {
            new InstallerWindow().Show();
            return;
        }

        var window = new MainWindow();
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log(e.Exception, "DispatcherUnhandled");
        MessageBox.Show($"A crash log was written to:\n{LogPath}\n\n{e.Exception.Message}",
            "Windows Tools crashed", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(1);
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log(ex, "DomainUnhandled");
            MessageBox.Show($"A crash log was written to:\n{LogPath}\n\n{ex.Message}",
                "Windows Tools crashed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void Log(Exception ex, string source)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} [{source}] ===");
            var e = ex;
            while (e != null)
            {
                sb.AppendLine($"{e.GetType().FullName}: {e.Message}");
                sb.AppendLine(e.StackTrace);
                sb.AppendLine("---");
                e = e.InnerException;
            }
            sb.AppendLine();
            File.AppendAllText(LogPath, sb.ToString());
        }
        catch { }
    }
}
