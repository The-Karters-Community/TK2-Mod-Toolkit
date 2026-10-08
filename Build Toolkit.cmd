@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\build_toolkit.ps1" %*
if errorlevel 1 (
    echo Build failed. Read the error above.
    pause
    exit /b 1
)
echo Toolkit executable and ZIP are ready in artifacts\portable.
pause
