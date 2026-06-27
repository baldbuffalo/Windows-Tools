using System.Diagnostics;
using System.IO;
using System.Net.Http;
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

        var result = await RunInstallAsync(app, progress, ct);
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
            Category = app.Category,
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

    private static async Task<(bool success, string? error)> RunInstallAsync(
        ManufacturerApp app, IProgress<string> progress, CancellationToken ct)
    {
        // Try winget first
        if (IsWingetAvailable())
        {
            progress.Report("Installing via winget...");
            return await RunWingetAsync(app.WingetId, ct);
        }

        // Winget missing — try to install it
        progress.Report("Windows Package Manager not found. Installing it...");
        if (await TryInstallWingetAsync(progress, ct))
        {
            progress.Report("Installing via winget...");
            return await RunWingetAsync(app.WingetId, ct);
        }

        // Last resort: open the manufacturer download page
        if (!string.IsNullOrEmpty(app.DownloadPageUrl))
        {
            progress.Report("Opening download page in browser...");
            Process.Start(new ProcessStartInfo { FileName = app.DownloadPageUrl, UseShellExecute = true });
            return (false, "Windows Package Manager could not be installed automatically. " +
                          "The download page has been opened in your browser.");
        }

        return (false, "Windows Package Manager (winget) is not available on this system. " +
                       "Please install it from the Microsoft Store (App Installer).");
    }

    private static bool IsWingetAvailable()
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task<bool> TryInstallWingetAsync(IProgress<string> progress, CancellationToken ct)
    {
        try
        {
            const string url = "https://github.com/microsoft/winget-cli/releases/latest/download/Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle";
            var path = Path.Combine(Path.GetTempPath(), "WindowsTools", "AppInstaller.msixbundle");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            progress.Report("Downloading Windows Package Manager...");
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "WindowsTools/1.0");
            var data = await http.GetByteArrayAsync(url, ct);
            await File.WriteAllBytesAsync(path, data, ct);

            progress.Report("Applying package...");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NonInteractive -Command \"Add-AppxPackage -Path '{path}'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            using var proc = Process.Start(psi)!;
            await proc.WaitForExitAsync(ct);

            try { File.Delete(path); } catch { }

            return IsWingetAvailable();
        }
        catch { return false; }
    }

    private static async Task<(bool success, string? error)> RunWingetAsync(string wingetId, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"install --id \"{wingetId}\" --accept-package-agreements --accept-source-agreements",
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
