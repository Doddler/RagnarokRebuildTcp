# Stop the RoBotClient dashboard if it's bound to port 5080.
$port = 5080
$pids = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty OwningProcess -Unique
foreach ($p in $pids) {
    try { Stop-Process -Id $p -Force -ErrorAction Stop } catch {}
}
if ($pids) {
    Write-Host "Stopped $($pids.Count) process(es) on port $port."
} else {
    Write-Host "Nothing listening on port $port."
}
