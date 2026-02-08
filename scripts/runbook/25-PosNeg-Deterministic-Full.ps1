# =========================
# PosNeg Runbook (deterministic sim) - FULL SEQUENCE
# =========================
# Generates a fresh dataset, trains PosNeg (micro), runs baseline vs PosNeg, then prints inspection commands.
#
# Default behavior keeps all artifacts UNDER THE REPO (results\_scratch\...), so you don't depend on %TEMP%.
#
# Usage (PowerShell):
#   cd C:\pg\RakeX
#   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
#   .\scripts\runbook\25-PosNeg-Deterministic-Full.ps1
#
# Optional:
#   .\scripts\runbook\25-PosNeg-Deterministic-Full.ps1 -RootMode temp
#
#
# Expected outcomes (what 'good' looks like):
#   - Script completes without errors and writes all artifacts under results\_scratch\EmbeddingShift.PosNeg\<timestamp>.
#   - A fresh dataset is generated (stage-00) and PosNeg training runs deterministically (sim/deterministic).
#   - Training reports a non-zero delta vector (|Δ| capped by clip if needed).
#   - Baseline vs PosNeg metrics are printed. The delta may be positive or negative depending on dataset shape/seed.
#
# Why this matters:
#   - It validates the full PosNeg workflow plumbing (generate -> train -> persist -> load 'best' -> run -> inspect/history).
#   - It provides a repeatable micro-run for debugging cancellations/volatility before doing larger sweeps.
[CmdletBinding()]
param(
  [ValidateSet('repo','temp')]
  [string]$RootMode = 'repo',

  [string]$Tenant   = 'insurer-a',
  [string]$DsName   = 'PosNegDS',
  [int]$Stages      = 1,
  [int]$Policies    = 30,
  [int]$Queries     = 60,
  [int]$Seed        = 1337,

  [ValidateSet('micro','production')]
  [string]$Mode     = 'micro'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$proj    = 'src\EmbeddingShift.ConsoleEval'
$backend = 'sim'
$simMode = 'deterministic'

# --- Clean start root ---
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
if ($RootMode -eq 'temp') {
  $root = Join-Path $env:TEMP ("EmbeddingShift.PosNeg\" + $stamp)
} else {
  $root = Join-Path $repoRoot ("results\_scratch\EmbeddingShift.PosNeg\" + $stamp)
}
if (Test-Path $root) { Remove-Item $root -Recurse -Force }
New-Item -ItemType Directory -Force -Path $root | Out-Null

$env:EMBEDDINGSHIFT_ROOT   = $root
$env:EMBEDDINGSHIFT_TENANT = $Tenant

Write-Host "[PosNeg] ROOT   = $env:EMBEDDINGSHIFT_ROOT"
Write-Host "[PosNeg] TENANT = $env:EMBEDDINGSHIFT_TENANT"
Write-Host "[PosNeg] MODE   = $backend/$simMode"

# --- 1) Generate dataset (stage-00) ---
dotnet run --project $proj -- `
  --tenant $Tenant `
  domain mini-insurance dataset-generate $DsName `
  --stages $Stages --policies $Policies --queries $Queries --seed $Seed --overwrite

# Point dataset root to stage-00 (what the mini-insurance flows expect)
$datasetRoot = Join-Path $env:EMBEDDINGSHIFT_ROOT ("results\insurance\tenants\{0}\datasets\{1}\stage-00" -f $Tenant, $DsName)
$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $datasetRoot
Write-Host "[PosNeg] DATASET_ROOT = $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT"

# --- 2) PosNeg TRAIN ---
dotnet run --project $proj -- `
  --tenant $Tenant --backend=$backend --sim-mode=$simMode `
  domain mini-insurance posneg-train --mode=$Mode

# --- 3) PosNeg RUN (baseline vs PosNeg shift) ---
dotnet run --project $proj -- `
  --tenant $Tenant --backend=$backend --sim-mode=$simMode `
  domain mini-insurance posneg-run --mode=$Mode

# --- 4) Inspect training results (latest + best) ---
dotnet run --project $proj -- `
  --tenant $Tenant `
  domain mini-insurance posneg-inspect

dotnet run --project $proj -- `
  --tenant $Tenant `
  domain mini-insurance posneg-history 10

dotnet run --project $proj -- `
  --tenant $Tenant `
  domain mini-insurance posneg-best

Write-Host ""
Write-Host "[PosNeg] DONE. Root: $env:EMBEDDINGSHIFT_ROOT"
