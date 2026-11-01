<#
.SYNOPSIS  Stops the services started by start-services.ps1 (by recorded PID only).
#>
$root = Split-Path -Parent $PSScriptRoot
$pidFile = Join-Path $root "artifacts/services.pids"
if (-not (Test-Path $pidFile)) { Write-Host "No services.pids file; nothing to stop."; return }
foreach ($id in Get-Content $pidFile) {
    $p = Get-Process -Id $id -ErrorAction SilentlyContinue
    if ($p -and $p.ProcessName -like "Lms.*") { Stop-Process -Id $id -Force; Write-Host "Stopped $($p.ProcessName) ($id)" }
}
Remove-Item $pidFile
