#requires -Version 5.1

# =========================
# PosNeg BIG MATRIX (deterministic sim)
# =========================
# Runs:
#   - Multi-seed AND multiple dataset sizes (policies/queries)
#   - Each combination uses the underlying 35-PosNegBigRunAll-Deterministic.ps1 runner
#   - Prep/Build only once (35 is invoked with -SkipPrep/-SkipBuild/-SkipTests/-SkipInspect)
#   - Tests + scratch layout inspection only once at the end
#   - Produces a consolidated summary via 37-PosNegBigSummarize.ps1
#
# Typical usage:
#   cd C:\pg\RakeX
#   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
#   Remove-Item -Recurse -Force .\results\_scratch -ErrorAction SilentlyContinue
#   .\scripts\runbook-experimental\36-PosNegBigRunMatrix-Deterministic.ps1 -Tenant insurer-a -Promote
#
[CmdletBinding()]
param(
  [ValidateSet('scratch','temp')]
  [string]$RootMode = 'scratch',

  [string]$Tenant = 'insurer-a',

  # Multi-seed run set
  [int[]]$Seeds = @(1337, 2026, 9001),

  # Size matrix format: "<policies>x<queries>"
  # Default runs: baseline-sized + large-sized
  [string[]]$SizeMatrix = @('500x1000', '2000x5000'),

  [int]$Stages = 3,
  [int[]]$StageIndices = @(0,1,2),

  [ValidateSet('micro','production','prod')]
  [string]$TrainMode = 'production',

  [int]$HardNegTopK = 10,
  [double[]]$PosNegScales = @(1.0, 2.0, 5.0),

  [string]$Metric = 'ndcg@3',
  [int]$Top = 10,

  [bool]$IncludeRepoPosNeg = $true,
  [bool]$CompareRepoPosNeg = $true,

  [switch]$Promote,

  # Isolate matrix runs under a dedicated scratch scenario folder.
  [string]$Scenario = 'EmbeddingShift.PosNegBigMatrix',

  # Optional cleanup helpers
  [switch]$CleanScenario,

  # Run tests + layout inspection at the end
  [switch]$SkipTests,
  [switch]$SkipInspect,

  # Simulation settings (deterministic)
  [string]$SimAlgo = 'semantic-hash',
  [int]$SimSemanticCharNGrams = 1
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot {
  param([string]$ScriptRoot)

  $candidates = @()
  $candidates += (Get-Location).Path
  $candidates += (Resolve-Path (Join-Path $ScriptRoot '..\..')).Path
  $candidates += (Resolve-Path (Join-Path $ScriptRoot '..\..\..')).Path
  $candidates = $candidates | Select-Object -Unique

  foreach ($c in $candidates) {
    if (Test-Path (Join-Path $c '.git') -PathType Container) { return $c }
    if (Test-Path (Join-Path $c 'EmbeddingShift.sln') -PathType Leaf) { return $c }
  }

  throw "Cannot resolve RepoRoot. Checked: $($candidates -join '; ')"
}

function Parse-Size {
  param([Parameter(Mandatory=$true)][string]$Size)

  $s = $Size.Trim()
  if ($s -match '^(?<p>\d+)x(?<q>\d+)$') {
    return @{
      Policies = [int]$Matches['p']
      Queries  = [int]$Matches['q']
    }
  }

  throw "Invalid SizeMatrix entry '$Size'. Expected '<policies>x<queries>' (e.g. '2000x5000')."
}

$repoRoot = Resolve-RepoRoot -ScriptRoot $PSScriptRoot
Set-Location $repoRoot

Write-Host "[PosNegMatrix] RepoRoot  = $repoRoot"
Write-Host "[PosNegMatrix] Tenant   = $Tenant"
Write-Host "[PosNegMatrix] Seeds    = $($Seeds -join ', ')"
Write-Host "[PosNegMatrix] Sizes    = $($SizeMatrix -join ', ')"
Write-Host "[PosNegMatrix] Scenario = $Scenario"

# Optional: remove the dedicated scenario folder (NOT the whole scratch root)
if ($CleanScenario -and ($RootMode -eq 'scratch')) {
  $scenarioRoot = Join-Path $repoRoot ("results\_scratch\{0}" -f $Scenario)
  Write-Host "[PosNegMatrix] Cleaning scenario folder: $scenarioRoot"
  Remove-Item -Recurse -Force -LiteralPath $scenarioRoot -ErrorAction SilentlyContinue
}

# Prep + Build once
& (Join-Path $repoRoot 'scripts\runbook\00-Prep.ps1')
& (Join-Path $repoRoot 'scripts\runbook\10-Build.ps1')

$runner35 = Join-Path $repoRoot 'scripts\runbook-experimental\35-PosNegBigRunAll-Deterministic.ps1'
if (-not (Test-Path -LiteralPath $runner35 -PathType Leaf)) {
  throw "Runner not found: $runner35"
}

foreach ($seed in $Seeds) {
  foreach ($size in $SizeMatrix) {
    $pq = Parse-Size -Size $size
    $policies = $pq.Policies
    $queries  = $pq.Queries

    Write-Host ""
    Write-Host "============================================================"
    Write-Host ("[PosNegMatrix] RUN  seed={0}  policies={1}  queries={2}" -f $seed, $policies, $queries)
    Write-Host "============================================================"

    $invoke = @{
      RootMode = $RootMode
      Tenant = $Tenant
      Seed = $seed

      DsName = 'PosNegBigDS'
      Stages = $Stages
      Policies = $policies
      Queries = $queries
      StageIndices = $StageIndices

      TrainMode = $TrainMode
      HardNegTopK = $HardNegTopK
      PosNegScales = $PosNegScales

      Metric = $Metric
      Top = $Top

      IncludeRepoPosNeg = $IncludeRepoPosNeg
      CompareRepoPosNeg = $CompareRepoPosNeg

      Scenario = $Scenario

      SkipPrep = $true
      SkipBuild = $true
      SkipTests = $true
      SkipInspect = $true

      SimAlgo = $SimAlgo
      SimSemanticCharNGrams = $SimSemanticCharNGrams
    }

    if ($Promote) { $invoke['Promote'] = $true }

    & $runner35 @invoke
    if ($LASTEXITCODE -ne 0) { throw "Runner 35 failed (exit=$LASTEXITCODE) for seed=$seed size=$size" }
  }
}

# Tests + Layout inspect once at the end
if (-not $SkipTests) {
  & (Join-Path $repoRoot 'scripts\runbook\30-Tests.ps1')
}

if (($RootMode -eq 'scratch') -and (-not $SkipInspect)) {
  & (Join-Path $repoRoot 'scripts\runbook\60-Inspect-ScratchLayout.ps1') -Scenario $Scenario
}

# Consolidated summary
$summaryScript = Join-Path $repoRoot 'scripts\runbook-experimental\37-PosNegBigSummarize.ps1'
if (Test-Path -LiteralPath $summaryScript -PathType Leaf) {
  & $summaryScript -Scenario $Scenario -Tenant $Tenant -Metric $Metric
}

Write-Host ""
Write-Host "[PosNegMatrix] DONE."
