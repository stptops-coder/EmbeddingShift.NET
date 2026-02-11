param(
  [string]$Tenant = "insurer-a",
  [int]$Seed = 1006,
  [switch]$All
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'


# Normalize paths for this run (prevents stale EMBEDDINGSHIFT_ROOT drift).
$envSummary = . (Join-Path $PSScriptRoot '01-EnvPaths.ps1') -Tenant $Tenant -Layout 'tenant' -Scenario 'EmbeddingShift.Sweep' -Force -ClearOptional -CreateFolders
Write-Host "[Paths] RepoRoot=$($envSummary.RepoRoot)"
Write-Host "[Paths] ResultsRoot=$($envSummary.ResultsRoot)"
Write-Host "[Paths] ActiveRunRoot=$($envSummary.ActiveRunRoot)"
Write-Host "[Paths] DatasetRoot=$($envSummary.DatasetRoot)"
if (-not [string]::IsNullOrWhiteSpace($envSummary.Tenant)) { Write-Host "[Paths] Tenant=$($envSummary.Tenant)" }

# Canonical "closing proof" suite:
#   - deterministic build
#   - deterministic acceptance sweep (compare/decide + optional promote)
#   - unit tests against samples
#
# Use -All to additionally run the older, repo-root based demos (FullRun/PosNeg/Segmenter).

& "$PSScriptRoot\00-Prep.ps1"
& "$PSScriptRoot\10-Build.ps1"

& "$PSScriptRoot\21-AcceptanceSweep-Deterministic.ps1" -Tenant $Tenant -Seed $Seed

& "$PSScriptRoot\90-Tests-Samples.ps1"

if ($All) {
  & "$PSScriptRoot\20-FullRun-MiniInsurance.ps1" -Tenant $Tenant -Seed $Seed
  & "$PSScriptRoot\30-PosNegRun-Scale10.ps1" -Tenant $Tenant
  & "$PSScriptRoot\40-Segment-Oracle.ps1" -Tenant $Tenant
  & "$PSScriptRoot\41-Segment-GapTau0.ps1" -Tenant $Tenant
}
