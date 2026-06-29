namespace WindowsTools.Models;

public enum AppCategory { DriverUpdater, OemSuite }

public class ManufacturerApp
{
    public AppCategory Category { get; set; } = AppCategory.OemSuite;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Icon { get; set; } = "🖥";
    public string Description { get; set; } = string.Empty;
    public string WingetId { get; set; } = string.Empty;
    public string[] ExeSearchPaths { get; set; } = [];
    public string? ShellLaunchArg { get; set; }
    public string? DownloadPageUrl { get; set; }
    // Direct link to the vendor's installer .exe. When set, it's downloaded and
    // run directly (real installer UI + UAC) instead of going through winget.
    public string? DirectInstallerUrl { get; set; }
    // Web UI to embed in-app (for browser-based tools like Intel DSA).
    public string? EmbedUrl { get; set; }
}
