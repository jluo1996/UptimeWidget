@echo off
setlocal

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"

set "PAYLOAD_DIR=%~2"
if "%PAYLOAD_DIR%"=="" set "PAYLOAD_DIR=%~dp0UptimeWidget\UptimeWidget\bin\x64\Release\net10.0-windows\"
if "%PAYLOAD_DIR:~-1%"=="\" set "PAYLOAD_DIR=%PAYLOAD_DIR:~0,-1%"

set "INSTALLER_NAME=%~3"

echo Building UptimeWidget installer with configuration: %CONFIG%
echo Payload directory: %PAYLOAD_DIR%
if not "%INSTALLER_NAME%"=="" echo Installer name: %INSTALLER_NAME%

rem === 1. Build the per-user MSI ===========================================
dotnet build "UptimeWidgetInstaller\UptimeWidgetInstaller\UptimeWidgetInstaller.wixproj" -c "%CONFIG%" -p:PayloadDir="%PAYLOAD_DIR%" -p:InstallerName="%INSTALLER_NAME%"
if errorlevel 1 (
    echo MSI build failed.
    pause
    exit /b %errorlevel%
)

set "MSI_PATH=%~dp0UptimeWidgetInstaller\UptimeWidgetInstaller\bin\%CONFIG%\en-US\UptimeWidgetInstaller.msi"
if not "%INSTALLER_NAME%"=="" set "MSI_PATH=%~dp0UptimeWidgetInstaller\UptimeWidgetInstaller\bin\%CONFIG%\en-US\%INSTALLER_NAME%.msi"

rem === 2. Fetch the .NET 10 Desktop Runtime (for RemotePayload harvest) =====
rem The runtime is still downloaded on demand at install time; this local copy
rem only supplies the size/hash metadata baked into the bundle.
set "RUNTIME_EXE=%~dp0UptimeWidgetInstaller\UptimeWidgetBootstrapper\obj\windowsdesktop-runtime-win-x64.exe"
set "RUNTIME_URL=https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"

if not exist "%~dp0UptimeWidgetInstaller\UptimeWidgetBootstrapper\obj" mkdir "%~dp0UptimeWidgetInstaller\UptimeWidgetBootstrapper\obj"
if not exist "%RUNTIME_EXE%" (
    echo Downloading .NET 10 Desktop Runtime for payload harvesting...
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest -Uri '%RUNTIME_URL%' -OutFile '%RUNTIME_EXE%'"
    if errorlevel 1 (
        echo Runtime download failed.
        pause
        exit /b %errorlevel%
    )
)

rem === 3. Build the bundle (.exe) wrapping the MSI + runtime prerequisite ====
dotnet build "UptimeWidgetInstaller\UptimeWidgetBootstrapper\UptimeWidgetBootstrapper.wixproj" -c "%CONFIG%" -p:MsiPath="%MSI_PATH%" -p:RuntimeExe="%RUNTIME_EXE%" -p:InstallerName="%INSTALLER_NAME%"
if errorlevel 1 (
    echo Bundle build failed.
    pause
    exit /b %errorlevel%
)

echo.
echo Installer bundle built successfully.
pause
exit /b 0