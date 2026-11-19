# Restart the RoBotClient dashboard on http://localhost:5080.
# - Kills any process currently bound to the port.
# - Rebuilds the solution (refreshes Bot + Web).
# - Relaunches the Web app detached, with launchSettings preserved (Development env => Static Web Assets
#   load, so the BotMap js/scoped-css ship correctly).
# - stdout/stderr from the dashboard land in dashboard.log / dashboard.err.log next to this script so
#   crashes are diagnosable. (Earlier `-WindowStyle Hidden` alone silently dropped stdout, which made
#   crashed-on-launch indistinguishable from "still booting".)

$ErrorActionPreference = "Continue"
$port = 5080
$root = "D:\Unity Projects\Ragnarok Rebuild\RagnarokRebuildTcp\Other\RoBotClient"
$logFile = Join-Path $root "dashboard.log"
$errFile = Join-Path $root "dashboard.err.log"

Write-Host "Stopping any process on port $port..."
$pids = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty OwningProcess -Unique
foreach ($p in $pids) {
    try { Stop-Process -Id $p -Force -ErrorAction Stop } catch {}
}
Start-Sleep -Seconds 2

Write-Host "Building solution..."
& dotnet build "$root\RoBotClient.sln" -nologo -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed (exit $LASTEXITCODE); aborting restart." -ForegroundColor Red
    exit 1
}

Write-Host "Launching dashboard on http://localhost:$port (background; output -> $logFile)..."
# Clear shell-local env so the launch profile's Development environment + applicationUrl override are honored
# correctly via the -- --urls app argument.
$env:ASPNETCORE_URLS = $null
$env:ASPNETCORE_ENVIRONMENT = $null

# Truncate the previous log so each restart starts with a clean slate. RedirectStandardOutput would fail if
# the file is held open by a previous, still-running invocation — but our Stop-Process above released it.
Remove-Item -Path $logFile -ErrorAction SilentlyContinue
Remove-Item -Path $errFile -ErrorAction SilentlyContinue

# Start-Process -ArgumentList as ARRAY splits each element on its internal whitespace when building the
# child's command line — so "$root\RoBotClient.Web\RoBotClient.Web.csproj" with spaces becomes multiple
# args and dotnet receives "D:\Unity" as the project. Use a SINGLE string with embedded double-quotes so
# CommandLineToArgvW keeps the path as one token.
$argLine = "run --no-build --project `"$root\RoBotClient.Web\RoBotClient.Web.csproj`" -- --urls http://localhost:$port"
Start-Process -FilePath "dotnet" `
    -ArgumentList $argLine `
    -WorkingDirectory $root `
    -RedirectStandardOutput $logFile `
    -RedirectStandardError $errFile `
    -WindowStyle Hidden

# Poll until Kestrel binds; if it doesn't, surface the tail of the log so the user can see why.
$deadline = (Get-Date).AddSeconds(45)
$bound = $false
while ((Get-Date) -lt $deadline) {
    if (Test-NetConnection -ComputerName localhost -Port $port -InformationLevel Quiet -WarningAction SilentlyContinue) {
        $bound = $true
        break
    }
    Start-Sleep -Milliseconds 800
}
if ($bound) {
    Write-Host "Dashboard up at http://localhost:$port"
} else {
    Write-Host "Port $port not bound after 45 s. Last lines of dashboard log:" -ForegroundColor Yellow
    if (Test-Path $logFile) { Get-Content $logFile -Tail 20 | ForEach-Object { "  $_" } }
    if (Test-Path $errFile) {
        Write-Host "stderr:" -ForegroundColor Yellow
        Get-Content $errFile -Tail 20 | ForEach-Object { "  $_" }
    }
    exit 1
}
