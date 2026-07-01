using System.Diagnostics;
using Microsoft.Win32;

namespace WindowsTools.Services;

public static class WindowsUpdateService
{
    private const string RunOnceKey = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string RunOnceName = "WindowsToolsWindowsUpdate";

    /// <summary>Launches the Windows Update page in Settings (so we can embed it).</summary>
    public static void OpenWindowsUpdate()
    {
        try { Process.Start(new ProcessStartInfo { FileName = "ms-settings:windowsupdate", UseShellExecute = true }); }
        catch { }
    }

    /// <summary>True when Windows is waiting for a restart to finish updates.</summary>
    public static bool IsRebootPending() =>
        KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired")
        || KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");

    /// <summary>
    /// Arms a one-time launch (HKCU RunOnce) so that after the next logon — i.e.
    /// once the restart is done — the app reopens straight to Windows Update.
    /// RunOnce deletes itself after firing, so it won't repeat on later startups.
    /// </summary>
    public static void ArmAutoOpen()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunOnceKey);
            key?.SetValue(RunOnceName, $"\"{InstallerService.InstallExePath}\" --windowsupdate");
        }
        catch { }
    }

    public static void DisarmAutoOpen()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunOnceKey, writable: true);
            key?.DeleteValue(RunOnceName, throwOnMissingValue: false);
        }
        catch { }
    }

    private static bool KeyExists(string path)
    {
        try { using var k = Registry.LocalMachine.OpenSubKey(path); return k is not null; }
        catch { return false; }
    }
}
