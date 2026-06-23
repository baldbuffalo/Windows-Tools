using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using WindowsTools.Models;

namespace WindowsTools.ViewModels;

public class StorageViewModel : INotifyPropertyChanged
{
    private bool _isLoading;
    private string _lastRefreshed = string.Empty;
    private long _totalSystemStorage;
    private long _totalUsedStorage;

    public ObservableCollection<DriveInfoModel> Drives { get; } = new();

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    public string LastRefreshed
    {
        get => _lastRefreshed;
        set { _lastRefreshed = value; OnPropertyChanged(); }
    }

    public string TotalSystemStorage => FormatBytes(_totalSystemStorage);
    public string TotalUsedStorage => FormatBytes(_totalUsedStorage);
    public string TotalFreeStorage => FormatBytes(_totalSystemStorage - _totalUsedStorage);

    public void Refresh()
    {
        IsLoading = true;
        Drives.Clear();

        try
        {
            long totalSize = 0;
            long totalUsed = 0;

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;

                var used = drive.TotalSize - drive.TotalFreeSpace;
                var usedPercent = drive.TotalSize > 0
                    ? (double)used / drive.TotalSize * 100
                    : 0;

                Drives.Add(new DriveInfoModel
                {
                    DriveLetter = drive.Name,
                    Label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                        ? GetDefaultLabel(drive.DriveType)
                        : drive.VolumeLabel,
                    DriveType = drive.DriveType.ToString(),
                    DriveTypeIcon = GetDriveIcon(drive.DriveType),
                    TotalSize = drive.TotalSize,
                    UsedSpace = used,
                    FreeSpace = drive.TotalFreeSpace,
                    UsedPercent = usedPercent,
                    FileSystem = drive.DriveFormat
                });

                if (drive.DriveType == System.IO.DriveType.Fixed)
                {
                    totalSize += drive.TotalSize;
                    totalUsed += used;
                }
            }

            _totalSystemStorage = totalSize;
            _totalUsedStorage = totalUsed;
            OnPropertyChanged(nameof(TotalSystemStorage));
            OnPropertyChanged(nameof(TotalUsedStorage));
            OnPropertyChanged(nameof(TotalFreeStorage));
        }
        finally
        {
            IsLoading = false;
            LastRefreshed = $"Last refreshed: {DateTime.Now:HH:mm:ss}";
        }
    }

    private static string GetDriveIcon(System.IO.DriveType type) => type switch
    {
        System.IO.DriveType.Fixed => "💾",
        System.IO.DriveType.Removable => "📱",
        System.IO.DriveType.CDRom => "💿",
        System.IO.DriveType.Network => "🌐",
        System.IO.DriveType.Ram => "⚡",
        _ => "🖥️"
    };

    private static string GetDefaultLabel(System.IO.DriveType type) => type switch
    {
        System.IO.DriveType.Fixed => "Local Disk",
        System.IO.DriveType.Removable => "Removable Drive",
        System.IO.DriveType.CDRom => "CD/DVD Drive",
        System.IO.DriveType.Network => "Network Drive",
        System.IO.DriveType.Ram => "RAM Disk",
        _ => "Unknown Drive"
    };

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_099_511_627_776L)
            return $"{bytes / 1_099_511_627_776.0:F2} TB";
        if (bytes >= 1_073_741_824L)
            return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576L)
            return $"{bytes / 1_048_576.0:F1} MB";
        return $"{bytes / 1024.0:F1} KB";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
