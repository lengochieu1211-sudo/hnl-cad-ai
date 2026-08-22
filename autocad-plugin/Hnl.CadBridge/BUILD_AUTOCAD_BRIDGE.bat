@echo off
setlocal
if "%AUTOCAD_SDK%"=="" (
  echo [ERROR] Set AUTOCAD_SDK to the AutoCAD managed DLL folder first.
  exit /b 1
)
if "%1"=="2023" goto OLD
if "%1"=="2024" goto OLD
if "%1"=="2025" goto NEW
if "%1"=="2026" goto NEW
echo Usage: BUILD_AUTOCAD_BRIDGE.bat 2023^|2024^|2025^|2026
exit /b 1
:OLD
dotnet build Hnl.CadBridge.AutoCAD2023-2024.csproj -c Release
exit /b %errorlevel%
:NEW
dotnet build Hnl.CadBridge.AutoCAD2025-2026.csproj -c Release
exit /b %errorlevel%
