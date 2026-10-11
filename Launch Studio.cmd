@echo off
cd /d "%~dp0"
where npm >nul 2>nul
if errorlevel 1 (
  echo Install Node.js with npm before starting the source checkout.
  pause
  exit /b 1
)
if not exist "node_modules\vite\bin\vite.js" (
  call npm ci
  if errorlevel 1 goto frontend_failed
)
call npm run build
if errorlevel 1 goto frontend_failed
start "" pythonw "%~dp0launch.py"
exit /b 0

:frontend_failed
echo Frontend setup or build failed. Studio was not opened with stale assets.
pause
exit /b 1
