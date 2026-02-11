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
  [string]$Tenant = 'insurer-a',

  [switch]$Promote,

  # Pass-through to the underlying sweep script (e.g. -Metric 'ndcg@3' -Top 10 -Policies 40,60 -Queries 80,120 ...)
  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$InputArgs = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
  $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
  if (-not (Test-Path $repoRoot)) { throw "Repo root not found: $repoRoot" }
  Set-Location $repoRoot

  $sweepScript = Join-Path $PSScriptRoot '21-AcceptanceSweep-Deterministic.ps1'
  if (-not (Test-Path $sweepScript)) { throw "Sweep script not found: $sweepScript" }

  $argsMap = @{
    RootMode = 'repo'
    Tenant   = $Tenant
  }

  if ($Promote.IsPresent) { $argsMap['Promote'] = $true }

  Write-Host "[Runbook21] TENANT  = $Tenant"
  Write-Host "[Runbook21] PROMOTE = $($Promote.IsPresent)"
  if ($InputArgs.Count -gt 0) {
    Write-Host "[Runbook21] PASS-THRU ARGS: $($InputArgs -join ' ')"
  }

  & $sweepScript @argsMap @InputArgs
}
catch {
  Write-Error $_
  exit 1
}
