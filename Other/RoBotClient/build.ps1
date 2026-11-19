# Build script for RoBotClient.
# Builds RoBotClient.Bot in-place and RoBotClient.Web to a temp dir so a running dashboard
# doesn't deadlock the build via its locked bin\Debug\net9.0\RoBotClient.Web.dll.
# Prints errors clearly with file:line and exits non-zero on failure so callers can chain.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$bot  = Join-Path $root 'RoBotClient.Bot\RoBotClient.Bot.csproj'
$web  = Join-Path $root 'RoBotClient.Web\RoBotClient.Web.csproj'
$tempWeb = Join-Path $env:TEMP 'robotwebcheck'

function Build-Project($proj, $extraArgs) {
    Write-Host "[build] $proj"
    $argList = @('build', $proj, '-nologo')
    if ($extraArgs) { $argList += $extraArgs }
    & dotnet @argList | Tee-Object -Variable out | Out-Null
    $errs = $out | Select-String -Pattern 'error '
    if ($LASTEXITCODE -ne 0 -or $errs) {
        Write-Host '--- ERRORS ---' -ForegroundColor Red
        $errs | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
        return $false
    }
    Write-Host "  OK" -ForegroundColor Green
    return $true
}

$okBot = Build-Project $bot $null
$okWeb = Build-Project $web @('-o', $tempWeb)

if (-not $okBot -or -not $okWeb) {
    Write-Host "build FAILED" -ForegroundColor Red
    exit 1
}

Write-Host "build OK (Bot + Web)" -ForegroundColor Green
exit 0
