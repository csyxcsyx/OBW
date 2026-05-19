@echo off
setlocal

echo This will remove OtpBridge startup entry and installed exe for the current user.
echo It will keep your config file unless you choose to delete it manually.
echo.
pause

reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v OtpBridge /f >nul 2>nul
taskkill /im OtpBridge.exe /f >nul 2>nul

if exist "%LOCALAPPDATA%\OtpBridge\OtpBridge.exe" del /f /q "%LOCALAPPDATA%\OtpBridge\OtpBridge.exe"

echo.
echo Uninstalled current-user OtpBridge executable.
echo Config remains at:
echo %APPDATA%\OtpBridge\config.json
echo.
pause
