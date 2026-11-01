<#
.SYNOPSIS  Starts the five benchmark services locally (no Docker) and waits until /health answers.
.NOTES     Ports: catalog 5101, members 5102, loans 5103, edge-rest 5201, edge-graphql 5202 (appsettings.json).
           Requires a prior build (scripts/build.ps1) and seeded LocalDB databases (scripts/db-init.ps1).
           Logs: benchmark/artifacts/logs/<service>.log ; PIDs: benchmark/artifacts/services.pids
#>
param([string]$Configuration = "Debug")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$logs = Join-Path $root "artifacts/logs"
New-Item -ItemType Directory -Force $logs | Out-Null
$pidFile = Join-Path $root "artifacts/services.pids"
if (Test-Path $pidFile) { Write-Warning "services.pids exists - run stop-services.ps1 first if services are still running." }

$services = @(
    @{ Name = "Lms.Catalog.Service"; Port = 5101 },
    @{ Name = "Lms.Members.Service"; Port = 5102 },
    @{ Name = "Lms.Loans.Service";   Port = 5103 },
    @{ Name = "Lms.Edge.Rest";       Port = 5201 },
    @{ Name = "Lms.Edge.GraphQL";    Port = 5202 }
)
$pids = @()
foreach ($s in $services) {
    $dir = Join-Path $root "src/$($s.Name)"
    $exe = Join-Path $dir "bin/$Configuration/net10.0/$($s.Name).exe"
    if (-not (Test-Path $exe)) { throw "Not built: $exe (run scripts/build.ps1)" }
    $p = Start-Process -FilePath $exe -WorkingDirectory $dir -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $logs "$($s.Name).log") -RedirectStandardError (Join-Path $logs "$($s.Name).err.log")
    $pids += $p.Id
}
$pids | Set-Content $pidFile

foreach ($s in $services) {
    $ok = $false
    for ($i = 0; $i -lt 60 -and -not $ok; $i++) {
        try { $r = Invoke-WebRequest -UseBasicParsing -TimeoutSec 2 "http://localhost:$($s.Port)/health"; $ok = $r.StatusCode -eq 200 }
        catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ok) { throw "$($s.Name) did not become healthy on port $($s.Port); see $logs" }
    Write-Host ("{0,-22} http://localhost:{1}  healthy" -f $s.Name, $s.Port)
}
