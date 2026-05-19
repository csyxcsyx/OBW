@echo off
setlocal
cd /d "%~dp0"

set SRC=%CD%\dist\OtpBridge\OtpBridge.exe
set TARGET=%LOCALAPPDATA%\OtpBridge
set TARGET_EXE=%TARGET%\OtpBridge.exe

if not exist "%SRC%" (
  echo Cannot find:
  echo %SRC%
  echo.
  echo Please run publish-win-x64.cmd first.
  pause
  exit /b 1
)

mkdir "%TARGET%" >nul 2>nul
copy /y "%SRC%" "%TARGET_EXE%" >nul

echo Installed to:
echo %TARGET_EXE%
echo.
start "" "%TARGET_EXE%"
pause
