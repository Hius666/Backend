@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Setup-Local.ps1"
if errorlevel 1 (
  echo.
  echo Chua khoi dong duoc API. Hay chup noi dung loi phia tren de kiem tra.
  pause
)
