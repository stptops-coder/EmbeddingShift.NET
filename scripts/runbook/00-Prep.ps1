Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Clears run-related environment variables (process scope) so runs are reproducible.
# This does NOT touch user/machine scope.

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Scratch  = Join-Path $RepoRoot 'results\_scratch'

Write-Host "[Prep] Clearing run-related environment variables (process scope)..."

$vars = @(
    'EMBEDDINGSHIFT_ROOT',
    'EMBEDDINGSHIFT_RESULTS_ROOT',
    'EMBEDDINGSHIFT_DATA_ROOT',
    'EMBEDDINGSHIFT_REPO_ROOT',
    'EMBEDDINGSHIFT_TENANT',
    'EMBEDDINGSHIFT_RESULTS_DOMAIN',
    'EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT',
    'EMBEDDINGSHIFT_SWEEP_ROOT',
    'EMBEDDINGSHIFT_RUNROOT',
    'EMBEDDINGSHIFT_EVAL_ROOT',
    'EMBEDDINGSHIFT_COMPARE_ROOT',
    'EMBEDDINGSHIFT_DECISION_ROOT',
    'EMBEDDINGSHIFT_DATASET_ROOT',
    'EMBEDDINGSHIFT_LAYOUT',
    'EMBEDDINGSHIFT_PROVIDER',
    'EMBEDDINGSHIFT_BACKEND',
    'EMBEDDINGSHIFT_SIM_MODE',
    'EMBEDDINGSHIFT_SIM_ALGO',
    'EMBEDDINGSHIFT_SIM_SEMANTIC_CHAR_NGRAMS',
    'EMBEDDING_SIM_NOISE_AMPLITUDE'
)

foreach ($v in $vars) {
    if (Test-Path "Env:$v") {
        Remove-Item "Env:$v" -ErrorAction SilentlyContinue
    }
}

New-Item -ItemType Directory -Force -Path $Scratch | Out-Null

Write-Host "[Prep] RepoRoot = $RepoRoot"
Write-Host "[Prep] Scratch  = $Scratch"
Write-Host "[Prep] OK"
