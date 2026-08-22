@echo off
setlocal
chcp 65001 >nul
title HNL CAD AI - Build Windows Installer
color 0B

echo ================================================================
echo          HNL CAD AI - WINDOWS 10/11 X64 INSTALLER BUILD
echo ================================================================
echo.

where node >nul 2>&1 || (
  echo [LOI] Chua co Node.js. Cai Node.js LTS x64 roi chay lai.
  pause & exit /b 1
)
where npm >nul 2>&1 || (
  echo [LOI] Khong tim thay npm.
  pause & exit /b 1
)

echo [1/5] Node: & node -v
echo [2/5] Cai/kiem tra dependency...
call npm install --no-audit --no-fund
if errorlevel 1 goto :fail

echo [3/5] Kiem tra TypeScript...
call npm run lint
if errorlevel 1 goto :fail

echo [4/5] Build ung dung...
call npm run build
if errorlevel 1 goto :fail

echo [5/5] Dong goi NSIS Installer Windows x64...
call npx electron-builder --win nsis --x64
if errorlevel 1 goto :fail

echo.
echo ================================================================
echo [THANH CONG]
echo File cai dat nam trong: dist_electron\
echo Ten du kien: HNL_CAD_AI_Setup_2.0.2.exe
echo ================================================================
pause
exit /b 0

:fail
echo.
echo [THAT BAI] Build bi loi. Xem dong loi phia tren.
pause
exit /b 1
