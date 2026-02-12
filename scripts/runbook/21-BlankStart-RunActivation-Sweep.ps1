# =========================
# Runbook 21 (Blank Start) – Run Activation Sweep (deterministic sim)
# =========================
# Convenience wrapper around:
#   scripts\runbook\21-AcceptanceSweep-Deterministic.ps1
#
# Purpose:
#   - Start a deterministic sweep with a clean run root.
#   - Optionally promote the decided best run.
#
# Usage:
#   cd C:\pg\RakeX
#   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
#   .\scripts\runbook\21-BlankStart-RunActivation-Sweep.ps1 -Tenant insurer-b
#   .\scripts\runbook\21-BlankStart-RunActivation-Sweep.ps1 -Tenant insurer-b -Promote
#
[CmdletBinding()]
param(
  [ValidateSet('repo','temp')]
  [string]$RootMode = 'repo',

  [string]$Tenant = 'insurer-a',

  [string]$DsName = 'SweepDS',
  [int]$Seed      = 1337,
  [int]$Stages    = 3,

  [int[]]$Policies = @(40, 60, 80),
  [int[]]$Queries  = @(80, 120),

  [string]$Metric = 'ndcg@3',
  [int]$Top       = 10,

  [switch]$Promote,

  # Additional pass-through to the underlying sweep script (rare; prefer explicit parameters above).
  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$InputArgs = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $repoRoot

$sweepScript = Join-Path $PSScriptRoot '21-AcceptanceSweep-Deterministic.ps1'
if (-not (Test-Path $sweepScript)) {
  throw "Sweep script not found: $sweepScript"
}

try {
  Write-Host "[Runbook21] TENANT  = $Tenant"
  Write-Host "[Runbook21] PROMOTE = $($Promote.IsPresent)"
  Write-Host "[Runbook21] ROOTMODE= $RootMode"
  Write-Host "[Runbook21] METRIC  = $Metric"
  if ($InputArgs.Count -gt 0) {
    Write-Host ("[Runbook21] PASS-THRU ARGS: " + ($InputArgs -join ' '))
  } else {
    Write-Host "[Runbook21] PASS-THRU ARGS: <none>"
  }

  $argsMap = @{
    RootMode = $RootMode
    Tenant   = $Tenant
    DsName   = $DsName
    Seed     = $Seed
    Stages   = $Stages
    Policies = $Policies
    Queries  = $Queries
    Metric   = $Metric
    Top      = $Top
  }

  if ($Promote.IsPresent) { $argsMap['Promote'] = $true }

  & $sweepScript @argsMap @InputArgs
}
catch {
  Write-Error $_
  exit 1
}
