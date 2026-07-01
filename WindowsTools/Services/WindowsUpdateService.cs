using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace WindowsTools.Services;

public class WindowsUpdateItem
{
    public required string Title { get; init; }
    public long SizeBytes { get; init; }
    public required object Update { get; init; } // COM IUpdate

    public string SizeText => SizeBytes <= 0
        ? ""
        : SizeBytes >= 1_000_000
            ? $"{SizeBytes / 1_048_576.0:0.0} MB"
            : $"{SizeBytes / 1024.0:0} KB";
}

/// <summary>
/// In-app Windows Update via the Windows Update Agent (WUA) COM API: search,
/// download and install updates without leaving the app. Installing requires
/// the process to be elevated. Also handles reboot detection + one-time
/// auto-reopen after the restart.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsUpdateService
{
    private const string RunOnceKey = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string RunOnceName = "WindowsToolsWindowsUpdate";

    public static Task<List<WindowsUpdateItem>> ScanAsync() => Task.Run(() =>
    {
        var list = new List<WindowsUpdateItem>();
        dynamic session = NewCom("Microsoft.Update.Session");
        dynamic searcher = session.CreateUpdateSearcher();
        dynamic result = searcher.Search("IsInstalled=0 and IsHidden=0");
        dynamic updates = result.Updates;
        for (var i = 0; i < (int)updates.Count; i++)
        {
            dynamic u = updates.Item(i);
            long size = 0;
            try { size = (long)u.MaxDownloadSize; } catch { }
            list.Add(new WindowsUpdateItem { Title = (string)u.Title, SizeBytes = size, Update = u });
        }
        return list;
    });

    public static Task<(bool ok, bool reboot, string? error)> InstallAsync(
        IEnumerable<WindowsUpdateItem> items, IProgress<string> progress) => Task.Run<(bool, bool, string?)>(() =>
    {
        try
        {
            dynamic session = NewCom("Microsoft.Update.Session");
            dynamic coll = NewCom("Microsoft.Update.UpdateColl");
            foreach (var it in items)
            {
                dynamic u = it.Update;
                try { if (!(bool)u.EulaAccepted) u.AcceptEula(); } catch { }
                coll.Add(u);
            }
            if ((int)coll.Count == 0) return (true, false, null);

            progress.Report("Downloading updates...");
            dynamic downloader = session.CreateUpdateDownloader();
            downloader.Updates = coll;
            downloader.Download();

            progress.Report("Installing updates...");
            dynamic installer = session.CreateUpdateInstaller();
            installer.Updates = coll;
            dynamic res = installer.Install();

            int code = (int)res.ResultCode; // 2 = Succeeded, 3 = Succeeded with errors
            bool reboot = (bool)res.RebootRequiredBeforeInstallation || (bool)res.RebootRequired;
            bool ok = code is 2 or 3;
            return (ok, reboot, ok ? null : $"Windows Update result {code}.");
        }
        catch (Exception ex)
        {
            return (false, false, ex.Message);
        }
    });

    /// <summary>True when Windows is waiting for a restart to finish updates.</summary>
    public static bool IsRebootPending() =>
        KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired")
        || KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");

    public static void RestartNow()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "/r /t 0",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch { }
    }

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

    private static object NewCom(string progId)
    {
        var type = Type.GetTypeFromProgID(progId)
            ?? throw new Exception($"{progId} is not available on this system.");
        return Activator.CreateInstance(type)!;
    }
}
