Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot([string]$from) {
  return (Resolve-Path (Join-Path $from "..\..")).Path
}

$RepoRoot = Resolve-RepoRoot $PSScriptRoot
Set-Location $RepoRoot

Write-Host "[VS-CleanEnv] RepoRoot = $RepoRoot"
Write-Host "[VS-CleanEnv] Clearing process-scope env vars..."
$names = @(
  "EMBEDDINGSHIFT_ROOT",
  "EMBEDDINGSHIFT_DATA_ROOT",
  "EMBEDDINGSHIFT_RESULTS_ROOT",
  "EMBEDDINGSHIFT_RESULTS_DOMAIN",
  "EMBEDDINGSHIFT_TENANT",
  "EMBEDDINGSHIFT_DATASET_ROOT",
  "EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT",
  "EMBEDDINGSHIFT_SIM_MODE",
  "EMBEDDINGSHIFT_SIM_ALGO",
  "EMBEDDINGSHIFT_SIM_SEMANTIC_CHAR_NGRAMS",
  "EMBEDDINGSHIFT_SIM_NOISE_AMPLITUDE",
  "EMBEDDING_SIM_NOISE_AMPLITUDE",
  "EMBEDDINGSHIFT_TEST_KEEP_ARTIFACTS"
)

foreach ($n in $names) {
  Remove-Item ("Env:\" + $n) -ErrorAction SilentlyContinue
}

Write-Host "[VS-CleanEnv] Clearing USER env vars (so VS starts clean next time)..."
foreach ($n in $names) {
  $v = [Environment]::GetEnvironmentVariable($n, "User")
  if (-not [string]::IsNullOrWhiteSpace($v)) {
    [Environment]::SetEnvironmentVariable($n, $null, "User")
    Write-Host "  removed User:$n"
  }
}

Write-Host "[VS-CleanEnv] NOTE: if Visual Studio is already running, restart it to pick up the clean environment."
