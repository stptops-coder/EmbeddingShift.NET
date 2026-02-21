#requires -Version 5.1
[CmdletBinding()]
param(
  [string]$Tenant = "insurer-a",
  [int]$Seed = 1006,
  [switch]$All
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

Write-Host "[RunAll] RepoRoot = $repoRoot"
Write-Host "[RunAll] Tenant  = $Tenant"
Write-Host "[RunAll] Seed    = $Seed"

& "$PSScriptRoot\00-Prep.ps1"
& "$PSScriptRoot\10-Build.ps1"

# Standard deterministic gate sweep (small & reproducible)
& "$PSScriptRoot\21-AcceptanceSweep-Deterministic.ps1" `
  -Tenant $Tenant `
  -Seed $Seed `
  -Policies 40 `
  -Queries 80 `
  -Stages 1 `
  -SimAlgo semantic-hash `
  -SimSemanticCharNGrams 1

# Full unit + acceptance suite (stable TEMP/TMP isolation happens inside this runner)
& (Join-Path $PSScriptRoot "..\runbook-internal\90-Tests-Samples.ps1")

if ($All) {
  & (Join-Path $PSScriptRoot "..\runbook-experimental\20-FullRun-MiniInsurance.ps1") -Tenant $Tenant -Seed $Seed
  & (Join-Path $PSScriptRoot "..\runbook-experimental\30-PosNegRun-Scale10.ps1") -Tenant $Tenant
  & (Join-Path $PSScriptRoot "..\runbook-experimental\40-Segment-Oracle.ps1") -Tenant $Tenant
  & (Join-Path $PSScriptRoot "..\runbook-experimental\41-Segment-GapTau0.ps1") -Tenant $Tenant
}
