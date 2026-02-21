Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $RepoRoot

# Ensure tests do NOT accidentally use a generated dataset
Remove-Item Env:\EMBEDDINGSHIFT_DATASET_ROOT -ErrorAction SilentlyContinue
Remove-Item Env:\EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT -ErrorAction SilentlyContinue

$env:EMBEDDINGSHIFT_DATASET_ROOT = (Join-Path $RepoRoot "samples\insurance")
$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $env:EMBEDDINGSHIFT_DATASET_ROOT

if (-not (Test-Path $env:EMBEDDINGSHIFT_DATASET_ROOT)) {
  throw "Samples dataset root not found: $env:EMBEDDINGSHIFT_DATASET_ROOT"
}

Write-Host ("[Tests] DATASET_ROOT=" + $env:EMBEDDINGSHIFT_DATASET_ROOT)

Write-Host "[Tests] Clearing run-related env vars (to keep acceptance tests stable)"
Remove-Item Env:EMBEDDINGSHIFT_TENANT -ErrorAction SilentlyContinue
Remove-Item Env:EMBEDDINGSHIFT_RESULTS_ROOT -ErrorAction SilentlyContinue
Remove-Item Env:EMBEDDINGSHIFT_RESULTS_DOMAIN -ErrorAction SilentlyContinue
Remove-Item Env:EMBEDDINGSHIFT_ROOT -ErrorAction SilentlyContinue
Remove-Item Env:EMBEDDINGSHIFT_DATA_ROOT -ErrorAction SilentlyContinue

Remove-Item Env:EMBEDDINGSHIFT_SIM_MODE -ErrorAction SilentlyContinue
Remove-Item Env:EMBEDDINGSHIFT_SIM_ALGO -ErrorAction SilentlyContinue
Remove-Item Env:EMBEDDINGSHIFT_SIM_SEMANTIC_CHAR_NGRAMS -ErrorAction SilentlyContinue
Remove-Item Env:EMBEDDINGSHIFT_SIM_NOISE_AMPLITUDE -ErrorAction SilentlyContinue
Remove-Item Env:EMBEDDING_SIM_NOISE_AMPLITUDE -ErrorAction SilentlyContinue

# Isolate test runs from the repo-wide embedding cache to avoid file-lock flakiness.
# Convention: EMBEDDINGSHIFT_ROOT points to an *active run root* (a folder that contains a nested "results" folder).
if ([string]::IsNullOrWhiteSpace($env:EMBEDDINGSHIFT_ROOT)) {
  $ts = Get-Date -Format "yyyyMMdd_HHmmss"
  $scratchRoot = Join-Path $RepoRoot "results\_scratch"
  $testsRunRoot = Join-Path (Join-Path $scratchRoot "tests-data") $ts
  $testsResultsRoot = Join-Path $testsRunRoot "results"
  New-Item -ItemType Directory -Force -Path $testsResultsRoot | Out-Null
  $env:EMBEDDINGSHIFT_ROOT = $testsRunRoot
}

Write-Host ("[Tests] EMBEDDINGSHIFT_ROOT=" + $env:EMBEDDINGSHIFT_ROOT)


# Route OS temp usage into the isolated test run root to avoid sporadic permission / file-lock issues.
$origTemp = $env:TEMP
$origTmp = $env:TMP
$testsTempRoot = Join-Path $env:EMBEDDINGSHIFT_ROOT "_tmp"
New-Item -ItemType Directory -Force -Path $testsTempRoot | Out-Null
$env:TEMP = $testsTempRoot
$env:TMP  = $testsTempRoot
Write-Host ("[Tests] TEMP=" + $env:TEMP)

try {
  dotnet test ".\src\EmbeddingShift.Tests\EmbeddingShift.Tests.csproj"
}
finally {
  if ([string]::IsNullOrWhiteSpace($origTemp)) { Remove-Item Env:TEMP -ErrorAction SilentlyContinue } else { $env:TEMP = $origTemp }
  if ([string]::IsNullOrWhiteSpace($origTmp))  { Remove-Item Env:TMP  -ErrorAction SilentlyContinue } else { $env:TMP  = $origTmp }
}
