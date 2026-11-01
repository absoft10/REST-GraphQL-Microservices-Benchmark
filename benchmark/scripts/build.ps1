<#
.SYNOPSIS  Restores and builds the solution (warnings are errors) and records toolchain/package versions (B-4 draft).
#>
param([string]$Configuration = "Debug")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet build LmsBenchmark.slnx -c $Configuration -nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed." }
    New-Item -ItemType Directory -Force (Join-Path $root "artifacts") | Out-Null
    $info = @("# BUILD_INFO (generated $((Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')))", "",
              "- SDK: $(dotnet --version)", "- Configuration: $Configuration", "", "## Resolved packages (top-level and transitive)", '```')
    $info += (dotnet list LmsBenchmark.slnx package --include-transitive)
    $info += '```'
    $info | Set-Content -Encoding utf8 (Join-Path $root "artifacts/BUILD_INFO.md")
    Write-Host "Build succeeded. Versions: artifacts/BUILD_INFO.md"
} finally { Pop-Location }
