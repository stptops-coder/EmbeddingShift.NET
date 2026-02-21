# =========================
# BlankStart -> RunActivation -> AcceptanceSweep
# =========================
# This runbook intentionally resets the environment to avoid residual state and then
# performs an acceptance sweep (deterministic) under a fresh RunRoot.

[CmdletBinding()]
param(
  [ValidateSet('scratch','repo','temp')]
  [string]$RootMode = 'scratch',

  [string]$Tenant = 'insurer-a',
  [string]$DsName = 'SweepDS',
  [int]$Seed = 1337,
  [int]$Stages = 3,

  [string]$Metric = 'ndcg@3',
  [int]$Top = 10,
  [switch]$Promote
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

Write-Host "[BlankStart] Clearing run-related environment variables (process scope)..."
. (Join-Path $PSScriptRoot "..\runbook\00-Prep.ps1") | Out-Host

Write-Host "[RunActivation] Running deterministic acceptance sweep..."
& (Join-Path $PSScriptRoot "..\runbook\21-AcceptanceSweep-Deterministic.ps1") `
  -RootMode $RootMode `
  -Tenant $Tenant `
  -DsName $DsName `
  -Seed $Seed `
  -Stages $Stages `
  -Metric $Metric `
  -Top $Top `
  -Promote:$Promote

Write-Host "[BlankStart] DONE"
