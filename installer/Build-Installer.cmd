@echo off
rem Double-click to build the release files (Setup.exe, the portable zip and SHA256SUMS.txt) into ..\artifacts.
rem Needs only the .NET 10 SDK; the Inno Setup compiler is fetched into ..\.tools by installer\get-inno.ps1.
rem To choose the version:  Build-Installer.cmd 1.2.3
setlocal
set "VERSION=%~1"
if "%VERSION%"=="" set "VERSION=0.1.0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-package.ps1" -Version %VERSION%
if errorlevel 1 (
  echo.
  echo The build failed - read the message above.
) else (
  echo.
  echo Done. The files are in %~dp0..\artifacts
)
pause
