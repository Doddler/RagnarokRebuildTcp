@echo off
setlocal

set "PATH=%USERPROFILE%\.dotnet;%PATH%"

cd /d "%~dp0..\RoRebuildServer\RoRebuildServer"

echo Launch Profile: %1
echo .NET SDK in use:
dotnet --version
echo ------------------------------------

dotnet run --launch-profile %1

echo ------------------------------------
pause
