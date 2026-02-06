param(
  [string]$Tenant      = "insurer-b",
  [string]$DatasetName = "SweepDS",
  [int]   $Seed        = 1337,
  [string]$Metric      = "ndcg@3",
  [int]   $Top         = 10,
  [switch]$DoRerun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $RepoRoot

$Project = "src\EmbeddingShift.ConsoleEval"

# Create an isolated run root (blank start)
$Stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$Root = Join-Path $env:TEMP ("EmbeddingShift.Sweep\" + $Stamp)

New-Item -ItemType Directory -Force -Path $Root | Out-Null

$env:EMBEDDINGSHIFT_ROOT = $Root
$env:EMBEDDINGSHIFT_TENANT = $Tenant

Write-Host "=== Config ==="
Write-Host ("RepoRoot: " + $RepoRoot)
Write-Host ("Project : " + $Project)
Write-Host ("Tenant  : " + $Tenant)
Write-Host ("Root    : " + $Root)
Write-Host ("Metric  : " + $Metric)
Write-Host ""

Write-Host "=== Help ==="
dotnet run --project $Project -- --help
Write-Host ""

Write-Host "=== Dataset (Generate) ==="
dotnet run --project $Project -- --tenant $Tenant domain mini-insurance dataset-generate $DatasetName --stages 3 --policies 40 --queries 80 --seed $Seed --overwrite
Write-Host ""

$tenantPart = if ([string]::IsNullOrWhiteSpace($Tenant)) { "" } else { "tenants\$Tenant\" }
$datasetRoot = Join-Path $Root ("results\insurance\" + $tenantPart + "datasets\$DatasetName\stage-00")
Write-Host "`n[DATASET_ROOT] $datasetRoot"

$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $DatasetRoot

Write-Host ("[DATASET_ROOT] " + $DatasetRoot)

# Sanity checks (avoid confusing downstream errors)
$policiesDir = Join-Path $DatasetRoot "policies"
$queriesFile = Join-Path $DatasetRoot "queries\queries.json"
if (-not (Test-Path $policiesDir)) { throw "Policies directory not found: $policiesDir" }
if (-not (Test-Path $queriesFile)) { throw "Queries file not found: $queriesFile" }

Write-Host ""

Write-Host "=== Pipeline (Mini-Insurance, no learned delta) ==="
if (-not [string]::IsNullOrWhiteSpace($QueryPolicyPath)) {
  dotnet run --project $Project -- --tenant $Tenant --backend=sim --sim-mode=deterministic domain mini-insurance pipeline --no-learned --query-policy="$QueryPolicyPath"
} else {
  dotnet run --project $Project -- --tenant $Tenant --backend=sim --sim-mode=deterministic domain mini-insurance pipeline --no-learned
}
Write-Host ""

$RunsRoot = Join-Path $Root ("results\insurance\tenants\" + $Tenant + "\runs")

Write-Host "=== Compare/Best/Decide/Promote (Run Activation) ==="
dotnet run --project $Project -- --tenant $Tenant runs-compare --runs-root="$RunsRoot" --metric="$Metric" --top=$Top --write
dotnet run --project $Project -- --tenant $Tenant runs-best    --runs-root="$RunsRoot" --metric="$Metric" --write
dotnet run --project $Project -- --tenant $Tenant runs-decide  --runs-root="$RunsRoot" --metric="$Metric" --epsilon=0.001
dotnet run --project $Project -- --tenant $Tenant runs-promote --runs-root="$RunsRoot" --metric="$Metric"
dotnet run --project $Project -- --tenant $Tenant runs-active  --runs-root="$RunsRoot" --metric="$Metric"
dotnet run --project $Project -- --tenant $Tenant runs-history --runs-root="$RunsRoot"
Write-Host ""

Write-Host "=== Promote again (History should grow) ==="
dotnet run --project $Project -- --tenant $Tenant runs-promote --runs-root="$RunsRoot" --metric="$Metric"
dotnet run --project $Project -- --tenant $Tenant runs-active  --runs-root="$RunsRoot" --metric="$Metric"
dotnet run --project $Project -- --tenant $Tenant runs-history --runs-root="$RunsRoot"
Write-Host ""

Write-Host "=== Rollback (Back to previous active) ==="
dotnet run --project $Project -- --tenant $Tenant runs-rollback --runs-root="$RunsRoot" --metric="$Metric"
dotnet run --project $Project -- --tenant $Tenant runs-active   --runs-root="$RunsRoot" --metric="$Metric"
dotnet run --project $Project -- --tenant $Tenant runs-history  --runs-root="$RunsRoot"
Write-Host ""

if ($DoRerun) {
  Write-Host "=== Rerun (Replay from best pointer) ==="

  $BestDir  = Join-Path $RunsRoot "_best"
  $BestFile = Get-ChildItem -Path $BestDir -Filter "best_*.json" |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

  if (-not $BestFile) {
    throw "Best pointer not found in: $BestDir (did runs-best --write succeed?)"
  }

  $BestPath = $BestFile.FullName
  Write-Host ("Using best pointer: " + $BestPath)

  $best = Get-Content $BestPath -Raw | ConvertFrom-Json
  if (-not $best.RunDirectory) {
    throw "Best pointer JSON does not contain RunDirectory: $BestPath"
  }

  $runDir = $best.RunDirectory
  Write-Host ("[runs-rerun] RunDirectory=" + $runDir)

  dotnet run --project $Project -- --tenant $Tenant runs-rerun --run-dir="$runDir"
  Write-Host ""
}

Write-Host "=== Done ==="
Write-Host ("Root: " + $Root)
Write-Host ("Runs: " + $RunsRoot)
