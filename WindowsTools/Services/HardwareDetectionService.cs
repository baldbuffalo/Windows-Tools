using System.Management;
using WindowsTools.Models;

namespace WindowsTools.Services;

public class HardwareDetectionService
{
    private static readonly Dictionary<string, ManufacturerApp> AppCatalog = new()
    {
        ["nvidia-app"] = new ManufacturerApp
        {
            Id = "nvidia-app", Name = "NVIDIA App", Manufacturer = "NVIDIA", Icon = "🟢",
            Description = "GPU drivers, overlay & tuning",
            WingetId = "Nvidia.GeForceExperience",
            ExeSearchPaths = [@"C:\Program Files\NVIDIA Corporation\NVIDIA GeForce Experience\NVIDIA GeForce Experience.exe"]
        },
        ["amd-software"] = new ManufacturerApp
        {
            Id = "amd-software", Name = "AMD Software: Adrenalin", Manufacturer = "AMD", Icon = "🔴",
            Description = "GPU drivers & Adrenalin Edition",
            WingetId = "AdvancedMicroDevices.AMDSoftwareAdrenalinEdition",
            ExeSearchPaths = [@"C:\Program Files\AMD\CNext\CNext\RadeonSoftware.exe"]
        },
        ["amd-ryzen-master"] = new ManufacturerApp
        {
            Id = "amd-ryzen-master", Name = "AMD Ryzen Master", Manufacturer = "AMD", Icon = "🔴",
            Description = "CPU overclocking & monitoring",
            WingetId = "AdvancedMicroDevices.RyzenMaster",
            ExeSearchPaths = [@"C:\Program Files\AMD\RyzenMaster\RyzenMaster.exe"]
        },
        ["intel-dsa"] = new ManufacturerApp
        {
            Id = "intel-dsa", Name = "Intel Driver & Support", Manufacturer = "Intel", Icon = "🔵",
            Description = "Automatic driver updates",
            WingetId = "Intel.IntelDriverAndSupportAssistant",
            ExeSearchPaths =
            [
                @"C:\Program Files (x86)\Intel Driver and Support Assistant\DSATray.exe",
                @"C:\Program Files\Intel Driver and Support Assistant\DSATray.exe"
            ]
        },
        ["intel-arc"] = new ManufacturerApp
        {
            Id = "intel-arc", Name = "Intel Arc Control", Manufacturer = "Intel", Icon = "🔵",
            Description = "Intel GPU drivers & tuning",
            WingetId = "Intel.ArcControl",
            ExeSearchPaths = [@"C:\Program Files\Intel\Intel(R) Arc Control\ArcControl.exe"]
        },
        ["lenovo-vantage"] = new ManufacturerApp
        {
            Id = "lenovo-vantage", Name = "Lenovo Vantage", Manufacturer = "Lenovo", Icon = "🖥",
            Description = "System health, drivers & settings",
            WingetId = "9WZDNCRFJ4MV",
            ShellLaunchArg = "lenovo-vantage:"
        },
        ["dell-supportassist"] = new ManufacturerApp
        {
            Id = "dell-supportassist", Name = "Dell SupportAssist", Manufacturer = "Dell", Icon = "🖥",
            Description = "System health & automatic drivers",
            WingetId = "Dell.SupportAssist",
            ExeSearchPaths = [@"C:\Program Files\Dell\SupportAssistAgent\bin\SupportAssist.exe"]
        },
        ["hp-support"] = new ManufacturerApp
        {
            Id = "hp-support", Name = "HP Support Assistant", Manufacturer = "HP", Icon = "🖥",
            Description = "System health & driver updates",
            WingetId = "HP.HPSupportAssistant",
            ExeSearchPaths =
            [
                @"C:\Program Files (x86)\Hewlett-Packard\HP Support Framework\HPSF.exe",
                @"C:\Program Files\HP\HP Support Framework\HPSF.exe"
            ]
        },
        ["asus-myasus"] = new ManufacturerApp
        {
            Id = "asus-myasus", Name = "MyASUS", Manufacturer = "ASUS", Icon = "🖥",
            Description = "System health, drivers & settings",
            WingetId = "ASUS.MyASUS",
            ShellLaunchArg = "myasus:"
        },
        ["msi-center"] = new ManufacturerApp
        {
            Id = "msi-center", Name = "MSI Center", Manufacturer = "MSI", Icon = "🖥",
            Description = "System tuning, drivers & lighting",
            WingetId = "MSI.MSICenter",
            ShellLaunchArg = "msi-center:"
        },
        ["acer-care"] = new ManufacturerApp
        {
            Id = "acer-care", Name = "Acer Care Center", Manufacturer = "Acer", Icon = "🖥",
            Description = "System health & driver updates",
            WingetId = "Acer.AcerCareCenter",
            ExeSearchPaths = [@"C:\Program Files (x86)\Acer\Care Center\Care Center.exe"]
        },
    };

    public (List<HardwareInfo> hardware, List<ManufacturerApp> apps) Detect()
    {
        var hardware = new List<HardwareInfo>();
        var appIds = new HashSet<string>();

        try
        {
            var cpuName = QueryFirst("Win32_Processor", "Name");
            var cpuMfr = QueryFirst("Win32_Processor", "Manufacturer") ?? string.Empty;
            hardware.Add(new HardwareInfo { Category = "CPU", Icon = "⚡", Name = cpuName ?? "Unknown CPU", Detail = cpuName ?? "" });

            if (cpuMfr.Contains("AMD", StringComparison.OrdinalIgnoreCase))
                appIds.Add("amd-ryzen-master");
            else if (cpuMfr.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                appIds.Add("intel-dsa");
        }
        catch { }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterCompatibility FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "Unknown GPU";
                var compat = obj["AdapterCompatibility"]?.ToString() ?? string.Empty;
                hardware.Add(new HardwareInfo { Category = "GPU", Icon = "🎮", Name = name, Detail = compat });

                if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || compat.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                    appIds.Add("nvidia-app");
                else if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                    appIds.Add("amd-software");
                else if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase) && name.Contains("Arc", StringComparison.OrdinalIgnoreCase))
                    appIds.Add("intel-arc");
            }
        }
        catch { }

        try
        {
            var sysMfr = QueryFirst("Win32_ComputerSystem", "Manufacturer") ?? string.Empty;
            var sysModel = QueryFirst("Win32_ComputerSystem", "Model") ?? string.Empty;
            hardware.Add(new HardwareInfo { Category = "System", Icon = "💻", Name = sysModel, Detail = sysMfr });

            if (sysMfr.Contains("Lenovo", StringComparison.OrdinalIgnoreCase))
                appIds.Add("lenovo-vantage");
            else if (sysMfr.Contains("Dell", StringComparison.OrdinalIgnoreCase))
                appIds.Add("dell-supportassist");
            else if (sysMfr.Contains("HP", StringComparison.OrdinalIgnoreCase) || sysMfr.Contains("Hewlett", StringComparison.OrdinalIgnoreCase))
                appIds.Add("hp-support");
            else if (sysMfr.Contains("ASUS", StringComparison.OrdinalIgnoreCase) || sysMfr.Contains("ASUSTeK", StringComparison.OrdinalIgnoreCase))
                appIds.Add("asus-myasus");
            else if (sysMfr.Contains("MSI", StringComparison.OrdinalIgnoreCase) || sysMfr.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase))
                appIds.Add("msi-center");
            else if (sysMfr.Contains("Acer", StringComparison.OrdinalIgnoreCase))
                appIds.Add("acer-care");
        }
        catch { }

        var apps = appIds
            .Where(AppCatalog.ContainsKey)
            .Select(id => AppCatalog[id])
            .ToList();

        return (hardware, apps);
    }

    private static string? QueryFirst(string wmiClass, string property)
    {
        using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
        foreach (ManagementObject obj in searcher.Get())
            return obj[property]?.ToString();
        return null;
    }
}
