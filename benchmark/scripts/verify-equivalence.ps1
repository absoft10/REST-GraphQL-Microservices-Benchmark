<#
.SYNOPSIS  Runs all five configurations against the RUNNING local services and checks equivalence with the
           dataset oracle read directly from the databases (M2 §2.3; required-bytes-spec.md).
.OUTPUTS   benchmark/artifacts/equivalence/<Label>/: bytes_summary.csv, flat_keys.txt, nested_keys.txt (C-6),
           crir_*.jcs.json, report.json, samples/*.response.json + *.exchange.txt
#>
param(
    [string]$Label = "kaggle-v3",
    [int]$FlatLimit = 100, [int]$NestedLimit = 10, [int]$Offset = 0,
    [string]$Server = "(localdb)\MSSQLLocalDB", [string]$Prefix = "lms_",
    [string]$Configuration = "Debug"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$tool = Join-Path $root "src/Lms.Bench.Tool/bin/$Configuration/net10.0/lms-bench.exe"
& $tool equivalence --rest http://localhost:5201 --graphql http://localhost:5202 --server $Server --prefix $Prefix `
    --flat-limit $FlatLimit --nested-limit $NestedLimit --offset $Offset --out (Join-Path $root "artifacts/equivalence/$Label")
if ($LASTEXITCODE) { throw "EQUIVALENCE FAILED" }
