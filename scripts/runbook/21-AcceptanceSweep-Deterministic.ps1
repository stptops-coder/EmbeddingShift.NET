# =========================
    # Acceptance Sweep (deterministic sim)
    # =========================
    # Runs a parameter sweep over dataset sizes, then compares/decides (and optionally promotes) the best run.
    #
    # Default behavior keeps all artifacts UNDER THE REPO (results\_scratch\...), so you don't depend on %TEMP%.
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
    #
    # Expected outcomes (what 'good' looks like):
    #   - Script completes without errors and writes all artifacts under results\_scratch\EmbeddingShift.Sweep\<timestamp>.
    #   - For each (policies,queries) grid point, the Mini-Insurance pipeline runs end-to-end:
    #       Baseline -> FirstShift -> First+Delta -> LearnedDelta (PosNeg) -> Compare -> Decide.
    #   - Compare output is written to runs\_compare\compare_<metric>_*.md/.json and a decision to runs\_decisions\decision_<metric>_*.md/.json.
    #
    # Why this matters:
    #   - It demonstrates that the repo can reliably produce *evidence* (metrics + decisions), not just run code.
    #   - It intentionally shows that the 'best' lever can vary with dataset shape/size (PosNeg can win or lose).
    #   - In the current Mini-Insurance generator/settings, FirstShift often ranks #1 for ndcg@3; that is a measurement reality, not a failure.
    [CmdletBinding()]
    param(
      [ValidateSet('repo','temp')]
      [string]$RootMode = 'repo',

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

    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    Set-Location $repoRoot

    $proj = 'src\EmbeddingShift.ConsoleEval'
    $backend = 'sim'
    $simMode = 'deterministic'

    # --- Clean start root ---
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    if ($RootMode -eq 'temp') {
      $root = Join-Path $env:TEMP ("EmbeddingShift.Sweep\" + $stamp)
    } else {
      $root = Join-Path $repoRoot ("results\_scratch\EmbeddingShift.Sweep\" + $stamp)
    }
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $root | Out-Null

    $env:EMBEDDINGSHIFT_ROOT   = $root
    $env:EMBEDDINGSHIFT_TENANT = $Tenant

    Write-Host "[Sweep] ROOT   = $env:EMBEDDINGSHIFT_ROOT"
    Write-Host "[Sweep] TENANT = $env:EMBEDDINGSHIFT_TENANT"
    Write-Host "[Sweep] MODE   = $backend/$simMode"
Write-Host ("[Sweep] PROMOTE= {0}" -f ([bool]$Promote))

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
        $datasetRoot = Join-Path $env:EMBEDDINGSHIFT_ROOT ("results\insurance\tenants\{0}\datasets\{1}\stage-00" -f $Tenant, $DsName)
        $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $datasetRoot
        Write-Host "[Sweep] DATASET_ROOT = $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT"

        # 3) Baseline pipeline (First / First+Delta depending on defaults in your CLI)
        dotnet run --project $proj -- `
          --tenant $Tenant --backend=$backend --sim-mode=$simMode `
          domain mini-insurance pipeline

        # 4) Compare + decide (+ optional promote)
        $runsRoot = Join-Path $env:EMBEDDINGSHIFT_ROOT ("results\insurance\tenants\{0}\runs" -f $Tenant)

        dotnet run --project $proj -- `
          --tenant $Tenant `
          runs-compare --runs-root $runsRoot --metric $Metric --top $Top --write

        dotnet run --project $proj -- `
          --tenant $Tenant `
          runs-decide --runs-root $runsRoot --metric $Metric --write

        if ($Promote) {
          dotnet run --project $proj -- `
            --tenant $Tenant `
            runs-promote --runs-root $runsRoot --metric $Metric
        }

        Write-Host "[Sweep] Done: policies=$p, queries=$q"
      }
    }

    Write-Host ""
    Write-Host "[Sweep] DONE. Root: $env:EMBEDDINGSHIFT_ROOT"
