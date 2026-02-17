Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $RepoRoot

# Ensure tests do NOT accidentally use a generated dataset
Remove-Item Env:\EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT -ErrorAction SilentlyContinue
$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = (Join-Path $RepoRoot "samples\insurance")

if (-not (Test-Path $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT)) {
  throw "Samples dataset root not found: $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT"
}

Write-Host ("[Tests] DATASET_ROOT=" + $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT)

Write-Host "[Tests] Clearing run-related env vars (to keep acceptance tests stable)"
Remove-Item Env:EMBEDDINGSHIFT_TENANT -ErrorAction SilentlyContinue
Remove-Item Env:EMBEDDINGSHIFT_RESULTS_ROOT -ErrorAction SilentlyContinue
Remove-Item Env:EMBEDDINGSHIFT_ROOT -ErrorAction SilentlyContinue
Remove-Item Env:EMBEDDINGSHIFT_RESULTS_DOMAIN -ErrorAction SilentlyContinue

# Isolate test runs from the repo-wide embedding cache to avoid file-lock flakiness
if ([string]::IsNullOrWhiteSpace($env:EMBEDDINGSHIFT_DATA_ROOT)) {
  $ts = Get-Date -Format "yyyyMMdd_HHmmss"
  $scratchRoot = Join-Path $RepoRoot "results\_scratch"
  $testsDataRoot = Join-Path (Join-Path $scratchRoot "tests-data") $ts
  New-Item -ItemType Directory -Force -Path $testsDataRoot | Out-Null
  $env:EMBEDDINGSHIFT_DATA_ROOT = $testsDataRoot
}
Write-Host ("[Tests] EMBEDDINGSHIFT_DATA_ROOT=" + $env:EMBEDDINGSHIFT_DATA_ROOT)

dotnet test ".\src\EmbeddingShift.Tests\EmbeddingShift.Tests.csproj"
