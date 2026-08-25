@echo off
setlocal
if "%~1"=="" (
  echo Keo-tha thu muc repo HNL CAD AI vao file BAT nay, hoac chay:
  echo PUSH_BUILD_GATE_TO_GITHUB.bat "C:\duong-dan\hnl-cad-ai"
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PUSH_BUILD_GATE_TO_GITHUB.ps1" -RepoPath "%~1"
set ERR=%ERRORLEVEL%
pause
exit /b %ERR%
