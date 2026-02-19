# =========================
# Acceptance Sweep (deterministic sim)
# =========================
# Runs a parameter sweep over dataset sizes, then compares/decides and optionally promotes the best run.
#
# Canonical behavior keeps all artifacts UNDER THE REPO (results\_scratch\...), so you don't depend on %TEMP%.
#
# Usage (PowerShell):
#   cd C:\pg\RakeX
#   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
#   .\scripts\runbook\21-AcceptanceSweep-Deterministic.ps1
#
# Optional:
#   .\scripts\runbook\21-AcceptanceSweep-Deterministic.ps1 -RootMode temp
#   .\scripts\runbook\21-AcceptanceSweep-Deterministic.ps1 -Promote
#
# Notes:
#   - In scratch mode, each run uses a fresh RunRoot. To enable reproducible "active" decisions across runs,
#     we maintain a shared active pointer at: results\_scratch\_active\<domain>\tenants\<tenant>\runs\_active\active_<metric>.json
#
[CmdletBinding()]
param(
  # Root placement:
  #   - scratch (default) / repo: results\_scratch under the repo
  #   - temp: %TEMP%\EmbeddingShift.Sweep
  [ValidateSet('scratch','repo','temp')]
  [string]$RootMode = 'scratch',

  [string]$Tenant   = 'insurer-a',
  [string]$DsName   = 'SweepDS',
  [int]$Seed        = 1337,
  [int]$Stages      = 3,

  # Sweep grid (edit once, keep stable)
  [int[]]$Policies  = @(40, 60, 80),
  [int[]]$Queries   = @(80, 120),

  # Compare/Decide/Promote
  [string]$Metric   = 'ndcg@3',
  [int]$Top         = 10,
  [switch]$Promote
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot {
  param([string]$ScriptRoot)

  $candidates = @()
  $candidates += (Get-Location).Path
  $candidates += (Resolve-Path (Join-Path $ScriptRoot '..\..\..')).Path
  $candidates += (Resolve-Path (Join-Path $ScriptRoot '..\..')).Path
  $candidates = $candidates | Select-Object -Unique

  foreach ($c in $candidates) {
    if (Test-Path (Join-Path $c '.git') -PathType Container) { return $c }
  }

  throw "Cannot resolve RepoRoot. Checked: $($candidates -join '; ')"
}

$repoRoot = Resolve-RepoRoot -ScriptRoot $PSScriptRoot
Set-Location $repoRoot

$proj    = 'src\EmbeddingShift.ConsoleEval'
$domain  = 'insurance'
$backend = 'sim'
$simMode = 'deterministic'

# --- Clean start root ---
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
if ($RootMode -eq 'temp') {
  $root = Join-Path $env:TEMP ("EmbeddingShift.Sweep\" + $stamp)
}
else {
  # 'scratch' is canonical; 'repo' is kept as a backward-compatible alias.
  $root = Join-Path $repoRoot ("results\_scratch\EmbeddingShift.Sweep\" + $stamp)
}

if (Test-Path $root) { Remove-Item $root -Recurse -Force }
New-Item -ItemType Directory -Force -Path $root | Out-Null

# Keep process environment coherent for follow-up scripts / PathAudit.
$env:EMBEDDINGSHIFT_ROOT           = $root
$env:EMBEDDINGSHIFT_RESULTS_DOMAIN = $domain
$env:EMBEDDINGSHIFT_LAYOUT         = 'tenant'
$env:EMBEDDINGSHIFT_TENANT         = $Tenant

$env:EMBEDDINGSHIFT_BACKEND        = $backend
$env:EMBEDDINGSHIFT_SIM_MODE       = $simMode
$env:EMBEDDINGSHIFT_SIM_ALGO       = 'sha256'

Write-Host "[Sweep] ROOT    = $env:EMBEDDINGSHIFT_ROOT"
Write-Host "[Sweep] DOMAIN  = $env:EMBEDDINGSHIFT_RESULTS_DOMAIN"
Write-Host "[Sweep] TENANT  = $env:EMBEDDINGSHIFT_TENANT"
Write-Host "[Sweep] MODE    = $backend/$simMode"
Write-Host ("[Sweep] PROMOTE = {0}" -f ([bool]$Promote))

foreach ($p in $Policies) {
  foreach ($q in $Queries) {

    Write-Host ""
    Write-Host "========================================="
    Write-Host ("[Sweep] policies={0}, queries={1}" -f $p, $q)
    Write-Host "========================================="

    # 1) Generate dataset (stage-00)
    dotnet run --project $proj -- `
      --tenant $Tenant `
      domain mini-insurance dataset-generate $DsName `
      --stages $Stages --policies $p --queries $q --seed $Seed --overwrite

    # 2) Point dataset root to stage-00 (what the mini-insurance flows expect)
    $datasetRoot = Join-Path $env:EMBEDDINGSHIFT_ROOT ("results\{0}\tenants\{1}\datasets\{2}\stage-00" -f $domain, $Tenant, $DsName)
    $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $datasetRoot
    Write-Host "[Sweep] DATASET_ROOT = $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT"

    # 3) End-to-end Mini-Insurance pipeline (Baseline -> FirstShift -> First+Delta -> LearnedDelta)
    dotnet run --project $proj -- `
      --tenant $Tenant --backend=$backend --sim-mode=$simMode `
      domain mini-insurance pipeline

    # 4) Compare + decide (+ optional promote)
    $runsRoot = Join-Path $env:EMBEDDINGSHIFT_ROOT ("results\{0}\tenants\{1}\runs" -f $domain, $Tenant)
    $activeDir = Join-Path $runsRoot '_active'
    $activeFileName = "active_{0}.json" -f $Metric
    $activeFile = Join-Path $activeDir $activeFileName

    $useSharedActive = ($RootMode -eq 'scratch')
    $sharedActiveDir = Join-Path $repoRoot ("results\_scratch\_active\{0}\tenants\{1}\runs\_active" -f $domain, $Tenant)
    $sharedActiveFile = Join-Path $sharedActiveDir $activeFileName

    if ($useSharedActive) {
      # Restore shared active pointer into this run's runsRoot BEFORE runs-decide,
      # so decisions become stable across separate scratch roots.
      New-Item -ItemType Directory -Force -Path $activeDir | Out-Null
      if (Test-Path -LiteralPath $sharedActiveFile -PathType Leaf) {
        Copy-Item -LiteralPath $sharedActiveFile -Destination $activeFile -Force
        Write-Host ("[Sweep] Shared active restored: {0}" -f $sharedActiveFile)
      }
    }

    dotnet run --project $proj -- `
      --tenant $Tenant `
      runs-compare --runs-root $runsRoot --metric $Metric --top $Top --write

    dotnet run --project $proj -- `
      --tenant $Tenant `
      runs-decide --runs-root $runsRoot --metric $Metric --write

    $doPromote = $false
    if ($Promote) {
      $decisionsDir = Join-Path $runsRoot '_decisions'
      $latestDecision = Get-ChildItem $decisionsDir -Filter ("decision_{0}_*.json" -f $Metric) -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1

      if ($null -eq $latestDecision) {
        Write-Host ("[Sweep] Promote skipped: no decision json found in {0}" -f $decisionsDir)
      }
      else {
        $decision = Get-Content $latestDecision.FullName -Raw | ConvertFrom-Json
        $action = $decision.Action
        if ($null -eq $action) { $action = $decision.action }

        # Convention observed in your logs: Action=0 means "Promote".
        $doPromote = (($action -eq 0) -or ($action -eq 'Promote'))
        if (-not $doPromote) {
          Write-Host ("[Sweep] Promote skipped (decision action={0})" -f $action)
        }
      }
    }

    if ($doPromote) {
      dotnet run --project $proj -- `
        --tenant $Tenant `
        runs-promote --runs-root $runsRoot --metric $Metric

      if ($useSharedActive) {
        New-Item -ItemType Directory -Force -Path $sharedActiveDir | Out-Null
        if (Test-Path -LiteralPath $activeFile -PathType Leaf) {
          Copy-Item -LiteralPath $activeFile -Destination $sharedActiveFile -Force
          Write-Host ("[Sweep] Shared active updated: {0}" -f $sharedActiveFile)
        }
      }
    }

    Write-Host "[Sweep] Done: policies=$p, queries=$q"
  }
}

Write-Host ""
Write-Host "[Sweep] DONE. Root: $env:EMBEDDINGSHIFT_ROOT"
