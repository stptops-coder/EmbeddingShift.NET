[CmdletBinding()]
param(
  [string] $Tenant = "insurer-b",
  [string] $Metric = "ndcg@3",
  [double] $Eps = 0.001
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$proj = "src/EmbeddingShift.ConsoleEval"
$spec = "scripts/run/matrix-miniinsurance.example.json"

. (Join-Path $PSScriptRoot "..\lib\RepoRoot.ps1")
$repoRoot = Get-RepoRoot -StartPath $PSScriptRoot
Set-Location $repoRoot

Write-Host "== Matrix run (tenant=$Tenant) =="
dotnet run --project $proj -- --tenant $Tenant --backend=sim --sim-mode=deterministic `
  runs-matrix --spec=$spec

Write-Host "== Decide (write artifacts) =="
dotnet run --project $proj -- --tenant $Tenant `
  runs-decide --metric=$Metric --eps=$Eps --write

Write-Host "== Decide+Apply (promote only if decision=Promote) =="
dotnet run --project $proj -- --tenant $Tenant `
  runs-decide --metric=$Metric --eps=$Eps --apply --write

Write-Host "== Done =="
