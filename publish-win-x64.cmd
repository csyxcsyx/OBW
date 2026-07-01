@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set OUT=%CD%\dist\OtpBridge
set ZIP=%CD%\dist\OtpBridge-Portable-win-x64.zip
set BUILD_ROOT=%CD%\dist\.build
set BUILD_OBJ=%BUILD_ROOT%\obj-%RANDOM%-%RANDOM%

echo Closing running OtpBridge, if any...
taskkill /im OtpBridge.exe /f >nul 2>nul

echo Cleaning output folder...
if exist "%OUT%" rmdir /s /q "%OUT%"
if exist "%ZIP%" del /f /q "%ZIP%"
if exist "%BUILD_ROOT%" rmdir /s /q "%BUILD_ROOT%"
if exist "%OUT%" (
  echo Cannot clean output folder. Please close OtpBridge.exe and any Explorer preview using this folder:
  echo %OUT%
  pause
  exit /b 1
)
mkdir "%OUT%" >nul 2>nul

if not exist "%OUT%" (
  echo Cannot create output folder:
  echo %OUT%
  pause
  exit /b 1
)

echo Publishing OtpBridge...
dotnet publish ".\OtpBridge\OtpBridge.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:NuGetAudit=false ^
  -p:BaseOutputPath=%BUILD_ROOT%\bin\ ^
  -p:BaseIntermediateOutputPath=%BUILD_OBJ%\ ^
  -p:MSBuildProjectExtensionsPath=%BUILD_OBJ%\ ^
  -o "%OUT%"

if errorlevel 1 (
  echo Publish failed.
  pause
  exit /b 1
)

if exist "%OUT%\*.pdb" del /f /q "%OUT%\*.pdb"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%ZIP%' -Force"
if exist "%BUILD_ROOT%" rmdir /s /q "%BUILD_ROOT%" >nul 2>nul

echo.
echo Publish output:
echo %OUT%\OtpBridge.exe
echo.
echo Portable zip:
echo %ZIP%
echo.
explorer "%OUT%"
pause
