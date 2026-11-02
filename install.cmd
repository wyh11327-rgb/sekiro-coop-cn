@echo off
rem Double-click this file to install Sekiro Seamless Co-op + unlimited Spirit Emblems.
rem Options are passed to install.ps1, e.g.:
rem   install.cmd -Password "our-secret" -PlayerName "Wolf"
setlocal
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
  echo.
  echo Installation failed. Read the message above.
)
pause
exit /b %RC%
