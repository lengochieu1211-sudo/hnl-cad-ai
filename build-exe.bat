@echo off
setlocal
cd /d "%~dp0"
for /f "delims=" %%V in ('node -p "require('./package.json').version"') do set HNL_VERSION=%%V
echo HNL CAD AI v%HNL_VERSION% - BUILD EXE
call node scripts/check-version-sync.mjs || exit /b 1
call npm run dist:win || exit /b 1
echo Ten du kien: HNL_CAD_AI_Setup_%HNL_VERSION%.exe
pause
