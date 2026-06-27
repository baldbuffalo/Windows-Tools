using System.Diagnostics;
using System.Security.Principal;

namespace WindowsTools.Services;

/// <summary>
/// Helpers for checking and acquiring administrator rights. Running the app
/// elevated lets winget (and the installers it launches) install manufacturer
/// apps silently, without a UAC prompt for every single package.
/// </summary>
public static class ElevationService
{
    public static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Relaunches the current exe elevated (triggers one UAC prompt).
    /// Returns true if an elevated process was started — the caller should then
    /// shut the current (non-elevated) process down. Returns false if the user
    /// declined the prompt or elevation failed.
    /// </summary>
    public static bool RestartAsAdmin(string? arguments = null)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (exe is null) return false;

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
                Verb = "runas"
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            // User declined the UAC prompt (or it failed) — caller falls back.
            return false;
        }
    }
}
