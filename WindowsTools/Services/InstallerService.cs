using System.Diagnostics;
using System.IO;

namespace WindowsTools.Services;

/// <summary>
/// Installs Windows Tools as a normal machine-wide Windows application.
/// The installed executable lives under Program Files rather than AppData or Temp.
/// </summary>
public static class InstallerService
{
    public const string AppDisplayName = "Windows Tools";

    public static string InstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Windows Tools");

    public static string InstallExePath => Path.Combine(InstallDir, "WindowsTools.exe");

    private static string DesktopShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        AppDisplayName + ".lnk");

    private static string StartMenuShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        AppDisplayName + ".lnk");

    /// <summary>True when the running exe lives in the Program Files install folder.</summary>
    public static bool IsRunningInstalled()
    {
        var current = Environment.ProcessPath;
        return current is not null &&
               string.Equals(Path.GetFullPath(current), Path.GetFullPath(InstallExePath),
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Copies the executable into the Program Files installation directory.</summary>
    public static bool CopyExe()
    {
        var source = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (source is null) return false;

        try { Directory.CreateDirectory(InstallDir); } catch { return false; }

        for (var attempt = 0; attempt < 12; attempt++)
        {
            try
            {
                File.Copy(source, InstallExePath, overwrite: true);
                return true;
            }
            catch (IOException)
            {
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(500);
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Creates desktop and Start Menu shortcuts pointing at Program Files.</summary>
    public static void CreateShortcuts()
    {
        CreateShortcut(DesktopShortcutPath, InstallExePath);
        CreateShortcut(StartMenuShortcutPath, InstallExePath);
    }

    public static string? Install()
    {
        if (!CopyExe()) return null;
        CreateShortcuts();
        return InstallExePath;
    }

    public static void CleanupOldVersion()
    {
        foreach (var name in new[] { "WindowsTools.old.exe", "WindowsTools.new.exe" })
        {
            try
            {
                var p = Path.Combine(InstallDir, name);
                if (File.Exists(p)) File.Delete(p);
            }
            catch { }
        }
    }

    public static void LaunchInstalled()
    {
        // Use Explorer to launch the installed copy from the normal shell context.
        // This prevents the elevated installer process from unnecessarily keeping
        // the main application elevated.
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{InstallExePath}\"",
            UseShellExecute = false
        });
    }

    private static void CreateShortcut(string shortcutPath, string targetExe)
    {
        try
        {
            var workingDir = Path.GetDirectoryName(targetExe) ?? InstallDir;
            var script =
                "$w = New-Object -ComObject WScript.Shell; " +
                $"$s = $w.CreateShortcut('{shortcutPath.Replace("'", "''")}'); " +
                $"$s.TargetPath = '{targetExe.Replace("'", "''")}'; " +
                $"$s.WorkingDirectory = '{workingDir.Replace("'", "''")}'; " +
                $"$s.IconLocation = '{targetExe.Replace("'", "''")}'; " +
                "$s.Save()";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit();
        }
        catch { }
    }
}
