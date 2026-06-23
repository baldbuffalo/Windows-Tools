namespace WindowsTools.Models;

public class DriveInfoModel
{
    public string DriveLetter { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DriveType { get; set; } = string.Empty;
    public string DriveTypeIcon { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long UsedSpace { get; set; }
    public long FreeSpace { get; set; }
    public double UsedPercent { get; set; }
    public string TotalSizeFormatted => FormatBytes(TotalSize);
    public string UsedSpaceFormatted => FormatBytes(UsedSpace);
    public string FreeSpaceFormatted => FormatBytes(FreeSpace);
    public string StatusColor => UsedPercent >= 90 ? "#FFF44336" : UsedPercent >= 75 ? "#FFFFC107" : "#FF4CAF50";
    public string FileSystem { get; set; } = string.Empty;

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
}
