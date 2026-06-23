# Windows Tools

A modern, all-in-one Windows system utility app built with WPF (.NET 8).

## Features

### ✅ Storage Checker
- View all drives (fixed, removable, network, CD/DVD)
- See used vs free space with a color-coded progress bar
  - 🟢 Green: under 75% used
  - 🟡 Yellow: 75–90% used
  - 🔴 Red: over 90% used
- Summary cards for total / used / free across all fixed drives
- One-click refresh

### 🔜 Coming Soon
- Performance Monitor (CPU, RAM, GPU)
- Privacy Settings
- Network Info
- Process Manager

## Requirements

- Windows 10 or later (x64)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build

```bash
# Restore and build
dotnet build WindowsTools.sln -c Release

# Run directly
dotnet run --project WindowsTools/WindowsTools.csproj
```

Or open `WindowsTools.sln` in Visual Studio 2022+.

## Tech Stack

- **WPF** (.NET 8, `net8.0-windows`)
- **MVVM** pattern (INotifyPropertyChanged, no third-party framework)
- Dark theme with custom control templates
