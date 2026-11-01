<#
.SYNOPSIS  Creates (or resets), seeds and verifies the three local LocalDB databases.
.DESCRIPTION
  DESTRUCTIVE for <Prefix>catalog, <Prefix>members, <Prefix>loans only (default lms_).
  Books are the TEMPORARY fixture (not research data) until the Kaggle dataset is verified (DECISION-004, O-7).
  Outputs: benchmark/data/generated/<Label>/{books,members,loans}.csv, manifest.json, data_freeze_<Label>.json
#>
param(
    [string]$Server = "(localdb)\MSSQLLocalDB",
    [string]$Prefix = "lms_",
    [string]$Label = "local-fixture",
    [UInt64]$Seed = 20261101,
    [int]$Books = 2000, [int]$Members = 10000, [int]$TargetMembers = 10, [int]$LoansPerTarget = 5,
    [string]$Configuration = "Debug"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$tool = Join-Path $root "src/Lms.Bench.Tool/bin/$Configuration/net10.0/lms-bench.exe"
if (-not (Test-Path $tool)) { throw "Not built: $tool (run scripts/build.ps1)" }
sqllocaldb start MSSQLLocalDB | Out-Null
$out = Join-Path $root "data/generated/$Label"

& $tool db reset --server $Server --prefix $Prefix; if ($LASTEXITCODE) { throw "reset failed" }
& $tool db seed --server $Server --prefix $Prefix --label $Label --seed $Seed --books-fixture $Books --members $Members `
    --target-members $TargetMembers --loans-per-target $LoansPerTarget --out $out; if ($LASTEXITCODE) { throw "seed failed" }
& $tool db verify --server $Server --prefix $Prefix --manifest (Join-Path $out "manifest.json") --out (Join-Path $out "data_freeze_$Label.json")
if ($LASTEXITCODE) { throw "verification FAILED" }
