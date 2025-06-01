@echo off
setlocal

set "PATH=%USERPROFILE%\.dotnet;%PATH%"

cd /d "%~dp0..\RoRebuildServer\RoRebuildServer"

echo .NET SDK in use:
dotnet --version
echo ------------------------------------

dotnet run

echo ------------------------------------
pause
