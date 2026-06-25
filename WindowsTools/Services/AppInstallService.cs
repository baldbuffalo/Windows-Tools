using System.Diagnostics;
using System.IO;
using WindowsTools.Models;

namespace WindowsTools.Services;

public class AppInstallService(SettingsService settings)
{
    private static readonly string[] DesktopPaths =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        @"C:\Users\Public\Desktop"
    ];

    public async Task<(bool success, string? exePath, string? error)> InstallAsync(
        ManufacturerApp app, IProgress<string> progress, CancellationToken ct)
    {
        progress.Report("Starting installation...");

        var result = await RunWingetAsync(app.WingetId, ct);
        if (!result.success)
            return (false, null, result.error);

        progress.Report("Cleaning up desktop shortcuts...");
        RemoveDesktopShortcuts(app.Name);

        progress.Report("Locating installed app...");
        var exePath = FindExe(app.ExeSearchPaths);

        var entry = new InstalledAppEntry
        {
            Id = app.Id,
            Name = app.Name,
            Icon = app.Icon,
            LaunchPath = exePath,
            ShellLaunchArg = app.ShellLaunchArg
        };
        settings.AddInstalledApp(entry);

        progress.Report("Done.");
        return (true, exePath, null);
    }

    public void LaunchApp(ManufacturerApp app)
    {
        try
        {
            if (!string.IsNullOrEmpty(app.ShellLaunchArg))
            {
                Process.Start(new ProcessStartInfo { FileName = app.ShellLaunchArg, UseShellExecute = true });
                return;
            }
            var exe = FindExe(app.ExeSearchPaths);
            if (exe is not null)
                Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
        }
        catch { }
    }

    private static async Task<(bool success, string? error)> RunWingetAsync(string wingetId, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"install --id \"{wingetId}\" --accept-package-agreements --accept-source-agreements --silent",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi) ?? throw new Exception("Failed to start winget.");
            await proc.WaitForExitAsync(ct);
            if (proc.ExitCode != 0)
            {
                var err = await proc.StandardError.ReadToEndAsync(ct);
                return (false, string.IsNullOrWhiteSpace(err) ? $"winget exited with code {proc.ExitCode}" : err);
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static void RemoveDesktopShortcuts(string appName)
    {
        var keywords = appName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in DesktopPaths)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var lnk in Directory.GetFiles(dir, "*.lnk"))
            {
                var fileName = Path.GetFileNameWithoutExtension(lnk);
                if (keywords.Any(k => fileName.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    try { File.Delete(lnk); } catch { }
                }
            }
        }
    }

    private static string? FindExe(string[] paths)
    {
        foreach (var p in paths)
            if (File.Exists(p)) return p;
        return null;
    }
}
