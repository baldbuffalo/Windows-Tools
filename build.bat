@echo off
echo Building Windows Tools...
echo.

dotnet publish WindowsTools\WindowsTools.csproj ^
  /p:PublishProfile=win-x64-release ^
  -c Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build FAILED.
    pause
    exit /b 1
)

echo.
echo Done! Executable is at: WindowsTools\publish\WindowsTools.exe
echo.
pause
