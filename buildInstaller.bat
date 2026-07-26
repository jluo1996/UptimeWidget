@echo off
setlocal

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"

set "PAYLOAD_DIR=%~2"
if "%PAYLOAD_DIR%"=="" set "PAYLOAD_DIR=%~dp0UptimeWidget\UptimeWidget\bin\x64\Release\net10.0-windows\"
if "%PAYLOAD_DIR:~-1%"=="\" set "PAYLOAD_DIR=%PAYLOAD_DIR:~0,-1%"

set "INSTALLER_NAME=%~3"
if "%INSTALLER_NAME%"=="" set "INSTALLER_NAME=UptimeWidgetInstaller"

echo Building UptimeWidget installer with configuration: %CONFIG%
echo Payload directory: %PAYLOAD_DIR%
if not "%INSTALLER_NAME%"=="" echo Installer name: %INSTALLER_NAME%

dotnet build "UptimeWidgetInstaller\UptimeWidgetInstaller\UptimeWidgetInstaller.wixproj" -c "%CONFIG%" -p:PayloadDir="%PAYLOAD_DIR%" -p:InstallerName="%INSTALLER_NAME%"

if errorlevel 1 exit /b %errorlevel%
exit /b 0