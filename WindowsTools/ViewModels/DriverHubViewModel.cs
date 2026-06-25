using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using WindowsTools.Models;
using WindowsTools.Services;

namespace WindowsTools.ViewModels;

public class AppViewModel : INotifyPropertyChanged
{
    private string _status = string.Empty;
    private bool _isInstalling;
    private bool _isInstalled;

    public ManufacturerApp App { get; }
    public AppInstallService InstallService { get; }
    public SettingsService Settings { get; }

    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public bool IsInstalling { get => _isInstalling; set { _isInstalling = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanInstall)); } }
    public bool IsInstalled { get => _isInstalled; set { _isInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanInstall)); } }
    public bool CanInstall => !IsInstalling && !IsInstalled;

    public RelayCommand InstallCommand { get; }
    public RelayCommand LaunchCommand { get; }

    public AppViewModel(ManufacturerApp app, AppInstallService installService, SettingsService settings)
    {
        App = app;
        InstallService = installService;
        Settings = settings;
        IsInstalled = settings.IsInstalled(app.Id);

        InstallCommand = new RelayCommand(async () =>
        {
            IsInstalling = true;
            Status = "Starting...";
            var progress = new Progress<string>(s => Status = s);
            var (success, _, error) = await installService.InstallAsync(app, progress, CancellationToken.None);
            IsInstalling = false;
            if (success)
            {
                IsInstalled = true;
                Status = "Installed";
            }
            else
            {
                Status = $"Failed: {error}";
                MessageBox.Show(error, "Install Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }, () => CanInstall);

        LaunchCommand = new RelayCommand(() => installService.LaunchApp(app), () => IsInstalled);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class DriverHubViewModel : INotifyPropertyChanged
{
    private bool _isScanning;

    public ObservableCollection<HardwareInfo> Hardware { get; } = [];
    public ObservableCollection<AppViewModel> RecommendedApps { get; } = [];

    public bool IsScanning { get => _isScanning; set { _isScanning = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasResults)); } }
    public bool HasResults => !IsScanning && RecommendedApps.Count > 0;

    private readonly HardwareDetectionService _detection;
    private readonly AppInstallService _install;
    private readonly SettingsService _settings;

    public DriverHubViewModel(HardwareDetectionService detection, AppInstallService install, SettingsService settings)
    {
        _detection = detection;
        _install = install;
        _settings = settings;
    }

    public async Task ScanAsync()
    {
        IsScanning = true;
        Hardware.Clear();
        RecommendedApps.Clear();

        var (hw, apps) = await Task.Run(_detection.Detect);

        foreach (var h in hw) Hardware.Add(h);
        foreach (var a in apps) RecommendedApps.Add(new AppViewModel(a, _install, _settings));

        IsScanning = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
