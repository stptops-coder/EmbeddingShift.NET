param(
  [string]$Tenant = "insurer-a",
  [int]$Seed = 1006,
  [switch]$All
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Canonical "closing proof" suite:
#   - deterministic build
#   - deterministic acceptance sweep (compare/decide + optional promote)
#   - unit tests against samples
#
# Use -All to additionally run the older, repo-root based demos (FullRun/PosNeg/Segmenter).

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot
Write-Host "[RunAll] RepoRoot = $repoRoot"
Write-Host "[RunAll] Tenant  = $Tenant"
Write-Host "[RunAll] Seed    = $Seed"

& "$PSScriptRoot\00-Prep.ps1"
& "$PSScriptRoot\10-Build.ps1"

& "$PSScriptRoot\21-AcceptanceSweep-Deterministic.ps1" -Tenant $Tenant -Seed $Seed -Policies 40 -Queries 80 -Stages 1 -SimAlgo semantic-hash -SimSemanticCharNGrams 1

& "$PSScriptRoot\90-Tests-Samples.ps1"

if ($All) {
  & "$PSScriptRoot\20-FullRun-MiniInsurance.ps1" -Tenant $Tenant -Seed $Seed
  & "$PSScriptRoot\30-PosNegRun-Scale10.ps1" -Tenant $Tenant
  & "$PSScriptRoot\40-Segment-Oracle.ps1" -Tenant $Tenant
  & "$PSScriptRoot\41-Segment-GapTau0.ps1" -Tenant $Tenant
}
