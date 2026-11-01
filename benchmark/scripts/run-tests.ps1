<#
.SYNOPSIS  Runs unit tests and LocalDB integration tests (no Docker). TRX results: benchmark/artifacts/test-results.
.NOTES     Integration tests drop/recreate lms_it_catalog, lms_it_members, lms_it_loans on (localdb)\MSSQLLocalDB.
           Stop running services first (scripts/stop-services.ps1): they lock build outputs.
#>
param([ValidateSet("all", "unit", "integration")][string]$Suite = "all")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
sqllocaldb start MSSQLLocalDB | Out-Null
$target = switch ($Suite) { "unit" { "tests/Lms.UnitTests" } "integration" { "tests/Lms.IntegrationTests" } default { "LmsBenchmark.slnx" } }
Push-Location $root
try {
    dotnet test $target -nologo --logger trx --results-directory artifacts/test-results
    if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
} finally { Pop-Location }
