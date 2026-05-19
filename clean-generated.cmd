@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo Closing running OtpBridge, if any...
taskkill /im OtpBridge.exe /f >nul 2>nul

echo.
echo This will remove generated build/publish folders only:
echo   %CD%\OtpBridge\bin
echo   %CD%\OtpBridge\obj
echo   %CD%\OtpBridge\.publish-obj
echo   %CD%\OtpBridge\.verify-obj
echo   %CD%\build-obj
echo   %CD%\dist
echo.

rmdir /s /q "%CD%\OtpBridge\bin" 2>nul
rmdir /s /q "%CD%\OtpBridge\obj" 2>nul
rmdir /s /q "%CD%\OtpBridge\.publish-obj" 2>nul
rmdir /s /q "%CD%\OtpBridge\.verify-obj" 2>nul
rmdir /s /q "%CD%\build-obj" 2>nul
rmdir /s /q "%CD%\dist" 2>nul

echo Clean finished. If any folder remains, close Explorer/terminal windows using it and run this script again.
echo.
pause
