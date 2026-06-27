using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace WindowsTools.Services;

public class UpdateInfo
{
    public bool UpdateAvailable { get; init; }
    public string LocalSha { get; init; } = "";
    public string? RemoteSha { get; init; }
    public string? DownloadUrl { get; init; }
    public long RemoteSize { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Compares the running exe's SHA-256 against the WindowsTools.exe asset in the
/// GitHub "latest" release and, if different, downloads and swaps it in place.
/// </summary>
public static class UpdateService
{
    private const string ApiUrl =
        "https://api.github.com/repos/baldbuffalo/Windows-Tools/releases/tags/latest";
    private const string AssetName = "WindowsTools.exe";

    public static string GetLocalSha256()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (exe is null || !File.Exists(exe)) return "";
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(exe);
            return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
        }
        catch { return ""; }
    }

    public static async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        var local = GetLocalSha256();
        try
        {
            using var http = NewClient();
            var json = await http.GetStringAsync(ApiUrl, ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("assets", out var assets))
                return new UpdateInfo { LocalSha = local, Error = "Release has no assets yet." };

            foreach (var a in assets.EnumerateArray())
            {
                if (!string.Equals(a.GetProperty("name").GetString(), AssetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var url = a.GetProperty("browser_download_url").GetString();
                var size = a.TryGetProperty("size", out var s) ? s.GetInt64() : 0;

                // GitHub exposes the asset's sha256 in the "digest" field.
                string? remoteSha = null;
                if (a.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String)
                {
                    var v = d.GetString();
                    if (v is not null && v.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                        remoteSha = v["sha256:".Length..].ToLowerInvariant();
                }

                bool available;
                if (remoteSha is not null && local.Length > 0)
                    available = !string.Equals(remoteSha, local, StringComparison.OrdinalIgnoreCase);
                else
                    available = size > 0 && SafeLocalSize() != size; // fallback when no digest

                return new UpdateInfo
                {
                    UpdateAvailable = available,
                    LocalSha = local,
                    RemoteSha = remoteSha,
                    DownloadUrl = url,
                    RemoteSize = size
                };
            }

            return new UpdateInfo { LocalSha = local, Error = $"No {AssetName} in the latest release." };
        }
        catch (Exception ex)
        {
            return new UpdateInfo { LocalSha = local, Error = ex.Message };
        }
    }

    /// <summary>
    /// Downloads the new exe, swaps it over the installed one, relaunches it.
    /// Returns null on success (caller should shut down), or an error string.
    /// </summary>
    public static async Task<string?> DownloadAndApplyAsync(
        string url, IProgress<double> progress, CancellationToken ct = default)
    {
        try
        {
            var dir = InstallerService.InstallDir;
            Directory.CreateDirectory(dir);
            var target = InstallerService.InstallExePath;
            var newPath = Path.Combine(dir, "WindowsTools.new.exe");
            var oldPath = Path.Combine(dir, "WindowsTools.old.exe");

            using (var http = NewClient())
            using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                var total = resp.Content.Headers.ContentLength ?? 0;
                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(newPath);

                var buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                    read += n;
                    if (total > 0) progress.Report((double)read / total * 100);
                }
            }

            // Windows allows renaming a running exe: move it aside, drop the new one in.
            try { if (File.Exists(oldPath)) File.Delete(oldPath); } catch { }
            if (File.Exists(target)) File.Move(target, oldPath);
            File.Move(newPath, target);

            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                WorkingDirectory = dir,
                UseShellExecute = true
            });
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static long SafeLocalSize()
    {
        try { return new FileInfo(Environment.ProcessPath!).Length; }
        catch { return -1; }
    }

    private static HttpClient NewClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.Add("User-Agent", "WindowsTools-Updater");
        c.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        return c;
    }
}
