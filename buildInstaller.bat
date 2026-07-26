@echo off
setlocal

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Release"

set "PAYLOAD_DIR=%~2"
if "%PAYLOAD_DIR%"=="" set "PAYLOAD_DIR=%~dp0UptimeWidget\UptimeWidget\bin\x64\Release\net10.0-windows"

echo Building UptimeWidget installer with configuration: %CONFIG%
echo Payload directory: %PAYLOAD_DIR%

dotnet build "UptimeWidgetInstaller\UptimeWidgetInstaller\UptimeWidgetInstaller.wixproj" -c "%CONFIG%" -p:PayloadDir="%PAYLOAD_DIR%"

if errorlevel 1 exit /b %errorlevel%
exit /b 0