param(
  [string]$Tenant = "insurer-a",
  [int]$Seed = 1006,
  [switch]$DoRerun,
  [switch]$All
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Canonical "closing proof" suite:
#   - deterministic build
#   - blank-start activation loop (compare/best/decide/promote/history/rollback + optional rerun)
#   - unit tests against samples
#
# Use -All to additionally run the older, repo-root based demos (FullRun/PosNeg/Segmenter).

& "$PSScriptRoot\00-Prep.ps1"
& "$PSScriptRoot\10-Build.ps1"

if ($DoRerun) {
  & "$PSScriptRoot\21-BlankStart-RunActivation-Sweep.ps1" -Tenant $Tenant -Seed $Seed -DoRerun
} else {
  & "$PSScriptRoot\21-BlankStart-RunActivation-Sweep.ps1" -Tenant $Tenant -Seed $Seed
}

& "$PSScriptRoot\90-Tests-Samples.ps1"

if ($All) {
  & "$PSScriptRoot\20-FullRun-MiniInsurance.ps1" -Tenant $Tenant -Seed $Seed
  & "$PSScriptRoot\30-PosNegRun-Scale10.ps1" -Tenant $Tenant
  & "$PSScriptRoot\40-Segment-Oracle.ps1" -Tenant $Tenant
  & "$PSScriptRoot\41-Segment-GapTau0.ps1" -Tenant $Tenant
}
