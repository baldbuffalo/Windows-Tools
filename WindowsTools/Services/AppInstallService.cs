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

    /// <summary>True if the app is already present (known to us, or its exe / Store entry exists).</summary>
    public bool IsAppInstalled(ManufacturerApp app)
    {
        if (settings.IsInstalled(app.Id)) return true;
        if (FindExe(app.ExeSearchPaths) is not null) return true;
        if (!string.IsNullOrEmpty(app.ShellLaunchArg) && IsStoreProtocolRegistered(app.ShellLaunchArg)) return true;
        return false;
    }

    // Store apps register a URI protocol (e.g. "lenovo-vantage:") under HKCR when installed.
    private static bool IsStoreProtocolRegistered(string shellArg)
    {
        try
        {
            var scheme = shellArg.TrimEnd(':');
            using var key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(scheme);
            return key?.GetValue("URL Protocol") is not null;
        }
        catch { return false; }
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
        // Preferred: download the vendor installer and run it directly so the
        // real installer UI and UAC prompt appear (no winget dependency).
        if (!string.IsNullOrEmpty(app.DirectInstallerUrl))
        {
            var direct = await RunDirectInstallerAsync(app.DirectInstallerUrl, progress, ct);
            if (direct.success) return direct;
            // fall through to winget if the direct download failed
        }

        // Make sure winget is present (install it if missing).
        if (!IsWingetAvailable())
        {
            progress.Report("Windows Package Manager not found. Installing it...");
            if (!await TryInstallWingetAsync(progress, ct))
                return OpenPageFallback(app, "Windows Package Manager isn't available.");
        }

        progress.Report("Launching the installer...");
        var result = await RunWingetAsync(app.WingetId, ct);
        if (result.success) return result;

        // winget couldn't install it — fall back to the manufacturer's page.
        return OpenPageFallback(app, $"Automatic install failed ({result.error}).");
    }

    private static async Task<(bool success, string? error)> RunDirectInstallerAsync(
        string url, IProgress<string> progress, CancellationToken ct)
    {
        try
        {
            progress.Report("Downloading the installer...");
            var path = Path.Combine(Path.GetTempPath(), "WindowsTools", $"driver-setup-{Guid.NewGuid():N}.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                http.DefaultRequestHeaders.Add("User-Agent", "WindowsTools/1.0");
                var data = await http.GetByteArrayAsync(url, ct);
                if (data.Length < 50_000) return (false, "Installer download was empty.");
                await File.WriteAllBytesAsync(path, data, ct);
            }

            progress.Report("Running the installer... (approve the Windows prompt)");
            // UseShellExecute so the installer shows its UI and triggers UAC normally.
            var proc = Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            if (proc is not null)
                await proc.WaitForExitAsync(ct);

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static (bool success, string? error) OpenPageFallback(ManufacturerApp app, string reason)
    {
        if (!string.IsNullOrEmpty(app.DownloadPageUrl))
        {
            try { Process.Start(new ProcessStartInfo { FileName = app.DownloadPageUrl, UseShellExecute = true }); } catch { }
            return (false, reason + " Opened the download page in your browser.");
        }
        return (false, reason);
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
            var source = InferSource(wingetId);
            // --interactive forces the package's own installer UI (so UAC shows).
            var interactive = source == "winget" ? " --interactive" : "";
            var psi = new ProcessStartInfo
            {
                FileName = "winget",
                // Visible window (no redirect) so winget can show its progress and
                // the package's own installer + UAC prompt appear normally.
                Arguments = $"install --id \"{wingetId}\" --exact --source {source}{interactive} " +
                            "--accept-package-agreements --accept-source-agreements",
                UseShellExecute = false,
                CreateNoWindow = false
            };
            using var proc = Process.Start(psi) ?? throw new Exception("Failed to start winget.");
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0
                ? (true, null)
                : (false, $"winget exited with code {proc.ExitCode}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // Microsoft Store product IDs are 12-char uppercase alphanumeric (e.g. 9WZDNCRFJ4MV).
    private static string InferSource(string id) =>
        id.Length == 12 && id.All(c => char.IsUpper(c) || char.IsDigit(c)) ? "msstore" : "winget";

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
