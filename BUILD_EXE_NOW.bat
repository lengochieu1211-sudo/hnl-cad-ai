@echo off
setlocal
cd /d "%~dp0"
for /f "delims=" %%V in ('node -p "require('./package.json').version"') do set HNL_VERSION=%%V
title HNL CAD AI v%HNL_VERSION% - Build EXE
echo ================================================
echo HNL CAD AI v%HNL_VERSION% - BUILD WINDOWS X64
echo ================================================
echo Required for CAD plugin: .NET SDK 8.x and .NET SDK 10.x
call npm install --no-audit --no-fund || exit /b 1
call node scripts/check-version-sync.mjs || exit /b 1
call npm run dist:win || exit /b 1
echo.
echo Build OK. Installer expected: dist_electron\HNL_CAD_AI_Setup_%HNL_VERSION%.exe
pause
