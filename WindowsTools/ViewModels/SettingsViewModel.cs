using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using WindowsTools.Services;

namespace WindowsTools.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly SettingsService _settings;

    public SettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        LocalHash = UpdateService.GetLocalSha256();

        CheckCommand = new RelayCommand(async () => await CheckAsync(), () => !IsBusy);
        UpdateCommand = new RelayCommand(async () => await UpdateAsync(), () => UpdateAvailable && !IsBusy);
    }

    // --- General settings (persisted) ---
    public bool CheckUpdatesOnStartup
    {
        get => _settings.CheckUpdatesOnStartup;
        set { _settings.CheckUpdatesOnStartup = value; OnPropertyChanged(); }
    }

    public bool AutoInstallDrivers
    {
        get => _settings.AutoInstallDrivers;
        set { _settings.AutoInstallDrivers = value; OnPropertyChanged(); }
    }

    // --- About / update info ---
    public string AppVersion => "v1.0.0";
    public string InstallPath => InstallerService.InstallExePath;

    public string LocalHash { get; }
    public string LocalHashShort => Shorten(LocalHash);

    private string _remoteHash = "—";
    public string RemoteHash { get => _remoteHash; set { _remoteHash = value; OnPropertyChanged(); OnPropertyChanged(nameof(RemoteHashShort)); } }
    public string RemoteHashShort => Shorten(RemoteHash);

    private string _updateStatus = "Click \"Check for updates\" to compare with the latest release.";
    public string UpdateStatus { get => _updateStatus; set { _updateStatus = value; OnPropertyChanged(); } }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged();
            CheckCommand.RaiseCanExecuteChanged();
            UpdateCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _updateAvailable;
    public bool UpdateAvailable
    {
        get => _updateAvailable;
        set { _updateAvailable = value; OnPropertyChanged(); UpdateCommand.RaiseCanExecuteChanged(); }
    }

    private bool _showProgress;
    public bool ShowProgress { get => _showProgress; set { _showProgress = value; OnPropertyChanged(); } }

    private double _downloadProgress;
    public double DownloadProgress { get => _downloadProgress; set { _downloadProgress = value; OnPropertyChanged(); } }

    private string? _downloadUrl;

    public RelayCommand CheckCommand { get; }
    public RelayCommand UpdateCommand { get; }

    private async Task CheckAsync()
    {
        IsBusy = true;
        UpdateAvailable = false;
        UpdateStatus = "Checking the latest release...";

        var info = await UpdateService.CheckAsync();

        if (info.Error is not null)
        {
            UpdateStatus = $"Couldn't check: {info.Error}";
            RemoteHash = "—";
        }
        else
        {
            RemoteHash = info.RemoteSha ?? "(not published)";
            _downloadUrl = info.DownloadUrl;
            UpdateAvailable = info.UpdateAvailable;
            UpdateStatus = info.UpdateAvailable
                ? "A new version is available."
                : "You're on the latest version.";
        }

        IsBusy = false;
    }

    private async Task UpdateAsync()
    {
        if (_downloadUrl is null) return;

        IsBusy = true;
        ShowProgress = true;
        DownloadProgress = 0;
        UpdateStatus = "Downloading update... 0%";

        var lastPercent = -1;
        var progress = new Progress<double>(p =>
        {
            DownloadProgress = p;
            var whole = (int)p;
            if (whole != lastPercent) // throttle UI churn to once per percent
            {
                lastPercent = whole;
                UpdateStatus = $"Downloading update... {whole}%";
            }
        });
        var (path, error) = await UpdateService.DownloadInstallerAsync(_downloadUrl, progress);

        if (path is not null)
        {
            DownloadProgress = 100;
            UpdateStatus = "Download complete. Launching installer...";
            // Launch off the UI thread — ShellExecute of the new exe can block briefly.
            await Task.Run(() => Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }));
            Application.Current.Shutdown(0);
        }
        else
        {
            UpdateStatus = $"Update failed: {error}";
            ShowProgress = false;
            IsBusy = false;
        }
    }

    private static string Shorten(string hash) =>
        string.IsNullOrEmpty(hash) || hash == "—" || hash.StartsWith('(')
            ? hash
            : hash.Length >= 16 ? hash[..16] + "…" : hash;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
