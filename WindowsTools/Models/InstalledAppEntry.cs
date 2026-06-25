using System.Text.Json.Serialization;
using WindowsTools.ViewModels;

namespace WindowsTools.Models;

public class InstalledAppEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? LaunchPath { get; set; }
    public string? ShellLaunchArg { get; set; }

    [JsonIgnore]
    public RelayCommand LaunchCommand => new(() =>
    {
        try
        {
            if (!string.IsNullOrEmpty(ShellLaunchArg))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ShellLaunchArg,
                    UseShellExecute = true
                });
            }
            else if (!string.IsNullOrEmpty(LaunchPath) && System.IO.File.Exists(LaunchPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = LaunchPath,
                    UseShellExecute = true
                });
            }
        }
        catch { }
    });
}
