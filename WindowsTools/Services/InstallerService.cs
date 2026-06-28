using System.Diagnostics;
using System.IO;

namespace WindowsTools.Services;

/// <summary>
/// Makes the single self-contained exe behave like an installer.
/// On first run (from Downloads, USB, etc.) it copies itself into a permanent
/// per-user install folder, drops a desktop shortcut, and relaunches the
/// installed copy. Running the already-installed copy is a no-op.
/// </summary>
public static class InstallerService
{
    public const string AppDisplayName = "Windows Tools";

    public static string InstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "WindowsTools");

    public static string InstallExePath => Path.Combine(InstallDir, "WindowsTools.exe");

    private static string DesktopShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        AppDisplayName + ".lnk");

    private static string StartMenuShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        AppDisplayName + ".lnk");

    /// <summary>True when the running exe lives in the install folder.</summary>
    public static bool IsRunningInstalled()
    {
        var current = Environment.ProcessPath;
        return current is not null &&
               string.Equals(Path.GetFullPath(current), Path.GetFullPath(InstallExePath),
                   StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copies this exe into the permanent install folder.
    /// Returns true on success.
    /// </summary>
    public static bool CopyExe()
    {
        var source = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (source is null) return false;

        try { Directory.CreateDirectory(InstallDir); } catch { return false; }

        // When updating, the previous installed exe may still be running and lock
        // the target — retry briefly until it exits and the file is free.
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

    /// <summary>Creates the desktop and Start Menu shortcuts.</summary>
    public static void CreateShortcuts()
    {
        CreateShortcut(DesktopShortcutPath, InstallExePath);
        CreateShortcut(StartMenuShortcutPath, InstallExePath);
    }

    /// <summary>
    /// Convenience: copy + shortcuts in one call (used as a silent fallback).
    /// Returns the installed exe path on success, null on failure.
    /// </summary>
    public static string? Install()
    {
        if (!CopyExe()) return null;
        CreateShortcuts();
        return InstallExePath;
    }

    /// <summary>Removes leftover files from a previous in-place update.</summary>
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

    /// <summary>Launches the installed copy in a new process.</summary>
    public static void LaunchInstalled()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = InstallExePath,
            WorkingDirectory = InstallDir,
            UseShellExecute = true
        });
    }

    private static void CreateShortcut(string shortcutPath, string targetExe)
    {
        try
        {
            var workingDir = Path.GetDirectoryName(targetExe) ?? InstallDir;
            // Use WScript.Shell via PowerShell — no extra dependencies needed.
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
