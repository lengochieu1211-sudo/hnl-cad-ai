@echo off
setlocal EnableExtensions
chcp 65001 >nul
title HNL CAD AI v2.0.2 - Build EXE
color 0B
cd /d "%~dp0"

echo ============================================================
echo HNL CAD AI v2.0.2 - BUILD WINDOWS 10/11 X64 INSTALLER
echo ============================================================
echo.

where node >nul 2>&1 || (
  echo [ERROR] Chua co Node.js x64.
  echo Cai Node.js 22 LTS roi chay lai file nay.
  pause
  exit /b 1
)

where npm >nul 2>&1 || (
  echo [ERROR] Khong tim thay npm.
  pause
  exit /b 1
)

echo [1/6] Node / npm
node -v
npm -v

echo.
echo [2/6] Cai dependencies
call npm install --no-audit --no-fund
if errorlevel 1 goto FAIL

echo.
echo [3/6] TypeScript audit
call npm run lint
if errorlevel 1 goto FAIL

echo.
echo [4/6] Vite + server build
call npm run build
if errorlevel 1 goto FAIL

echo.
echo [5/6] Electron Builder NSIS x64
call npx electron-builder --win nsis --x64
if errorlevel 1 goto FAIL

echo.
echo [6/6] Kiem tra installer
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
 "$f=Get-ChildItem '.\dist_electron\HNL_CAD_AI_Setup_*.exe' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1; if(-not $f){throw 'Khong tim thay installer'}; if($f.Length -lt 5MB){throw 'Installer qua nho'}; $h=Get-FileHash $f.FullName -Algorithm SHA256; Write-Host ''; Write-Host 'BUILD OK'; Write-Host ('EXE: '+$f.FullName); Write-Host ('SIZE: '+[math]::Round($f.Length/1MB,2)+' MB'); Write-Host ('SHA256: '+$h.Hash)"
if errorlevel 1 goto FAIL

echo.
echo ============================================================
echo THANH CONG - Mo thu muc dist_electron de lay file .exe
echo ============================================================
start "" "%CD%\dist_electron"
pause
exit /b 0

:FAIL
echo.
echo ============================================================
echo BUILD THAT BAI - xem dong loi ngay phia tren
echo ============================================================
pause
exit /b 1
