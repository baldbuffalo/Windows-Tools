# Windows Tools

Windows Tools is a Windows desktop utility built with WinUI 3 (Windows App SDK).

## Download

Get WindowsTools.exe from the latest release: https://github.com/baldbuffalo/Windows-Tools/releases/tag/latest

It is an unpackaged, self-contained single-file executable. No MSIX installation or separate .NET / Windows App SDK runtime installation is required. Dependencies are extracted to a temporary directory on first launch.

On first run, you can install it under Program Files and create desktop / Start menu shortcuts, or run it directly without installing.

## Features

- Storage: inspect drive capacity, free space, and usage.
- Driver Hub: detect CPU, GPU, and system manufacturer; open manufacturer support pages or install matched tools with winget.
- Windows Update: open Windows Update in Windows Settings.
- Settings: configure startup update checks and check the latest release.

## Build

Build on Windows with the .NET 8 SDK using:
dotnet publish WindowsTools.WinUI/WindowsTools.WinUI.csproj -c Release -r win-x64 --self-contained true -o ./output

The previous WPF implementation remains in WindowsTools/ for reference during the migration. Release builds now use the WinUI 3 project in WindowsTools.WinUI/.
