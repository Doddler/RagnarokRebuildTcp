@echo off
REM Build both RoBotClient.Bot and RoBotClient.Web via PowerShell so the call is allowlisted under
REM one stable command line. Exit code propagates so a calling script can stop on failure.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
exit /b %ERRORLEVEL%
