@echo off
chcp 65001 >nul
set /p REPO=Nhap duong dan repo GitHub local (vd D:\GitHub\hnl-cad-ai-26): 
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0REPLACE_WHOLE_REPO.ps1" -RepoPath "%REPO%"
pause
