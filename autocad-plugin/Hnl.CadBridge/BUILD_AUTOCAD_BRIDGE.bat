@echo off
setlocal
cd /d "%~dp0"

set HAS_DOTNET_SDK=
for /f "delims=" %%S in ('dotnet --list-sdks 2^>nul') do set HAS_DOTNET_SDK=1
if not defined HAS_DOTNET_SDK (
  echo [ERROR] Missing .NET SDK. Install .NET SDK 8.x or use GitHub Actions.
  exit /b 1
)

if "%1"=="" (
  node ..\..\scripts\build-autocad-bundle.mjs
  exit /b %errorlevel%
)

if "%1"=="2023" goto BUILD_2023
if "%1"=="2024" goto BUILD_2024
if "%1"=="2025" goto BUILD_2025
if "%1"=="2026" goto BUILD_2026
if "%1"=="2027" goto BUILD_2027
echo Usage: BUILD_AUTOCAD_BRIDGE.bat [2023^|2024^|2025^|2026^|2027]
exit /b 1

:BUILD_2023
set PROJECT=Hnl.CadBridge.AutoCAD2023.csproj
set OUT=..\HNL.CadBridge.bundle\Contents\2023
goto BUILD_ONE

:BUILD_2024
set PROJECT=Hnl.CadBridge.AutoCAD2024.csproj
set OUT=..\HNL.CadBridge.bundle\Contents\2024
goto BUILD_ONE

:BUILD_2025
set PROJECT=Hnl.CadBridge.AutoCAD2025.csproj
set OUT=..\HNL.CadBridge.bundle\Contents\2025
goto BUILD_ONE

:BUILD_2026
set PROJECT=Hnl.CadBridge.AutoCAD2026.csproj
set OUT=..\HNL.CadBridge.bundle\Contents\2026
goto BUILD_ONE

:BUILD_2027
set PROJECT=Hnl.CadBridge.AutoCAD2027.csproj
set OUT=..\HNL.CadBridge.bundle\Contents\2027
goto BUILD_ONE

:BUILD_ONE
if not exist "%OUT%" mkdir "%OUT%"
dotnet restore "%PROJECT%" || exit /b 1
dotnet build "%PROJECT%" -c Release --no-restore -o "%OUT%" || exit /b 1
if not exist "%OUT%\Hnl.CadBridge.dll" (
  echo [ERROR] Missing output DLL: %OUT%\Hnl.CadBridge.dll
  exit /b 1
)
echo AutoCAD %1 bridge built: %OUT%\Hnl.CadBridge.dll
exit /b 0
