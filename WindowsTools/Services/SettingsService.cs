using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using WindowsTools.Models;

namespace WindowsTools.Services;

public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowsTools", "settings.json");

    public ObservableCollection<InstalledAppEntry> InstalledApps { get; } = [];

    private bool _checkUpdatesOnStartup = true;
    private bool _autoInstallDrivers = true;
    private string? _lastDriverEmbedUrl;

    public bool CheckUpdatesOnStartup
    {
        get => _checkUpdatesOnStartup;
        set { _checkUpdatesOnStartup = value; Save(); }
    }

    public bool AutoInstallDrivers
    {
        get => _autoInstallDrivers;
        set { _autoInstallDrivers = value; Save(); }
    }

    // Cached so Driver Hub can show the site instantly without waiting for detection.
    public string? LastDriverEmbedUrl
    {
        get => _lastDriverEmbedUrl;
        set { _lastDriverEmbedUrl = value; Save(); }
    }

    public SettingsService() => Load();

    public void AddInstalledApp(InstalledAppEntry entry)
    {
        if (InstalledApps.Any(a => a.Id == entry.Id)) return;
        InstalledApps.Add(entry);
        Save();
    }

    public bool IsInstalled(string id) => InstalledApps.Any(a => a.Id == id);

    private void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            var data = JsonSerializer.Deserialize<SettingsData>(json);
            if (data is null) return;
            _checkUpdatesOnStartup = data.CheckUpdatesOnStartup;
            _autoInstallDrivers = data.AutoInstallDrivers;
            _lastDriverEmbedUrl = data.LastDriverEmbedUrl;
            if (data.InstalledApps is not null)
                foreach (var entry in data.InstalledApps)
                    InstalledApps.Add(entry);
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var data = new SettingsData
            {
                InstalledApps = [.. InstalledApps],
                CheckUpdatesOnStartup = _checkUpdatesOnStartup,
                AutoInstallDrivers = _autoInstallDrivers,
                LastDriverEmbedUrl = _lastDriverEmbedUrl
            };
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private class SettingsData
    {
        public List<InstalledAppEntry> InstalledApps { get; set; } = [];
        public bool CheckUpdatesOnStartup { get; set; } = true;
        public bool AutoInstallDrivers { get; set; } = true;
        public string? LastDriverEmbedUrl { get; set; }
    }
}
