# Check whether the RoBotClient dashboard is listening on port 5080. Prints a one-line status.
$port = 5080
if (Test-NetConnection -ComputerName localhost -Port $port -InformationLevel Quiet -WarningAction SilentlyContinue) {
    Write-Host "Dashboard up at http://localhost:$port"
} else {
    Write-Host "Dashboard not listening on port $port."
}
