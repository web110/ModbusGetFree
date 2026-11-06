@echo off
cd /d "%~dp0.."
if exist "AiDemo.exe" (
    start "" "AiDemo.exe" %*
) else (
    if /I "%~1"=="--lang" (
        powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-demo.ps1" -Language "%~2"
    ) else (
        powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0start-demo.ps1"
    )
)
