Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $RepoRoot

# Defaults (keep them stable)
$tenant = "insurer-a"
$seed   = 1006

Write-Host "[FullRun] tenant=$tenant seed=$seed"
.\scripts\run\Run-MiniInsurance-SemHashNgrams1.ps1 -Tenant $tenant -Seed $seed

Write-Host ("[FullRun] DATASET_ROOT=" + $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT)

# Sanity checks (these prevent the 'Policies directory not found' confusion)
$policiesDir = Join-Path $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT "policies"
$queriesFile = Join-Path $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT "queries\queries.json"

if (-not (Test-Path $policiesDir)) { throw "Policies directory not found: $policiesDir" }
if (-not (Test-Path $queriesFile)) { throw "Queries file not found: $queriesFile" }

Write-Host "[FullRun] Dataset root looks OK (policies + queries.json exist)."
