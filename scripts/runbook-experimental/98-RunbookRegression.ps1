[CmdletBinding()]
param(
  [string]$Tenant = 'insurer-a',
  [string]$Metric = 'ndcg@3',
  [int]$Seed = 1006,

  # Optional: run the legacy/experimental scripts as well.
  [switch]$IncludeLegacy,

  # Optional: reserved for future layout validators.
  # Note: currently unused (kept for backward-compatible CLI usage).
  [switch]$ValidateJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
  $git = Get-Command git -ErrorAction SilentlyContinue
  if ($null -ne $git) {
    try {
      $r = (& git rev-parse --show-toplevel 2>$null)
      if (-not [string]::IsNullOrWhiteSpace($r)) { return $r.Trim() }
    } catch { }
  }
  return (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Assert-File([string]$path) {
  if (-not (Test-Path -LiteralPath $path)) { throw "Missing file: $path" }
}

function Invoke-Runbook([string]$name, [string]$relPath, [string[]]$args, [string]$logRoot) {
  $repoRoot = Get-RepoRoot
  $scriptPath = Join-Path $repoRoot $relPath
  Assert-File $scriptPath

  $logPath = Join-Path $logRoot ($name + '.log')
  Write-Host ""
  Write-Host ("=== RUN {0} ===" -f $name)
  Write-Host ("[Runner] Script: {0}" -f $scriptPath)
  if ($args.Count -gt 0) {
    Write-Host ("[Runner] Args  : {0}" -f ($args -join ' '))
  } else {
    Write-Host ("[Runner] Args  : <none>")
  }
  Write-Host ("[Runner] Log   : {0}" -f $logPath)

  # Run in a fresh process to avoid environment variable bleed.
  & powershell -NoProfile -ExecutionPolicy Bypass -File $scriptPath @args 2>&1 | Tee-Object -FilePath $logPath
  if ($LASTEXITCODE -ne 0) { throw "FAILED: $name (see $logPath)" }
}

$RepoRoot = Get-RepoRoot
$ResultsRoot = Join-Path $RepoRoot 'results'
$logRoot = Join-Path $ResultsRoot ("_scratch\RunbookRegression\" + (Get-Date -Format "yyyyMMdd_HHmmss"))
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null

Write-Host "[Regression] RepoRoot    = $RepoRoot"
Write-Host "[Regression] ResultsRoot = $ResultsRoot"
Write-Host "[Regression] LogRoot     = $logRoot"
Write-Host "[Regression] Tenant      = $Tenant"
Write-Host "[Regression] Metric      = $Metric"
Write-Host "[Regression] Seed        = $Seed"
Write-Host "[Regression] IncludeLegacy = $IncludeLegacy"
Write-Host "[Regression] ValidateJson  = $ValidateJson"

# Core coverage chain:
# - 99-RunAll: build + deterministic acceptance sweep (no promote) + tests
# - 21-Sweep-Promote: minimal promote path (shared active update)
# - 20-FullRun: dataset generate + pipeline + compare/decide
# - 40-Health: sanity report over recent results
Invoke-Runbook -name '99-RunAll' -relPath 'scripts\runbook\99-RunAll.ps1' -args @('-Tenant', $Tenant, '-Seed', "$Seed") -logRoot $logRoot
Invoke-Runbook -name '21-Sweep-Promote' -relPath 'scripts\runbook\21-AcceptanceSweep-Deterministic.ps1' -args @('-Tenant', $Tenant, '-Seed', "$Seed", '-Policies', '40', '-Queries', '80', '-Stages', '1', '-Metric', $Metric, '-Promote') -logRoot $logRoot
Invoke-Runbook -name '20-FullRun' -relPath 'scripts\runbook-experimental\20-FullRun-MiniInsurance.ps1' -args @('-Tenant', $Tenant, '-Seed', "$Seed") -logRoot $logRoot
Invoke-Runbook -name '40-Health' -relPath 'scripts\runbook-experimental\40-Health.ps1' -args @() -logRoot $logRoot

# Optional: legacy/experimental scripts (segmenter PoC, long runs, private dependencies).
if ($IncludeLegacy) {
  Invoke-Runbook -name '25-PosNeg-Full' -relPath 'scripts\runbook-experimental\25-PosNeg-Deterministic-Full.ps1' -args @('-Tenant', $Tenant) -logRoot $logRoot
  Invoke-Runbook -name '30-PosNeg-Scale10' -relPath 'scripts\runbook-experimental\30-PosNegRun-Scale10.ps1' -args @('-Tenant', $Tenant) -logRoot $logRoot
  Invoke-Runbook -name '40-Segment-Oracle' -relPath 'scripts\runbook-experimental\40-Segment-Oracle.ps1' -args @('-Tenant', $Tenant) -logRoot $logRoot
  Invoke-Runbook -name '41-Segment-GapTau0' -relPath 'scripts\runbook-experimental\41-Segment-GapTau0.ps1' -args @('-Tenant', $Tenant) -logRoot $logRoot
}

# Post-run: inspect latest scratch layouts.
if ($ValidateJson) {
  Write-Host "[Regression] Note: -ValidateJson is currently unused by 60-Inspect-ScratchLayout.ps1."
}

Invoke-Runbook -name 'Inspect-Sweep' -relPath 'scripts\runbook\60-Inspect-ScratchLayout.ps1' -args @('-Scenario','EmbeddingShift.Sweep') -logRoot $logRoot
Invoke-Runbook -name 'Inspect-MiniInsurance' -relPath 'scripts\runbook\60-Inspect-ScratchLayout.ps1' -args @('-Scenario','EmbeddingShift.MiniInsurance') -logRoot $logRoot

Write-Host ""
Write-Host "[Regression] OK. Logs: $logRoot"
exit 0
