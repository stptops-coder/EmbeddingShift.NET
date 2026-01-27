
param(
  [Parameter(Mandatory = $true)]
  [string] $Tenant,

  [Parameter(Mandatory = $true)]
  [ValidateSet('v1','v2','v3','v4')]
  [string] $Profile,

  [Parameter(Mandatory = $true)]
  [string] $DatasetName,

  [int] $Seed = 1006,

  # Option A (simple size-based generator)
  [ValidateSet('small','medium','large')]
  [string] $Size = 'large',

  # Option B (explicit generator) – if any of these is set, we use explicit mode
  [int] $Stages = 3,
  [int] $Policies = 0,
  [int] $QueryCount = 0,

  [switch] $Overwrite,

  [ValidateSet('production','micro')]
  [string] $TrainMode = 'production',

  [string] $Metric = 'ndcg@3',

  [switch] $NoBuild,

  # If set: do NOT call dataset-generate; assumes dataset already exists on disk
  [switch] $SkipGenerate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot "..\lib\RepoRoot.ps1")
$repoRoot = Get-RepoRoot -StartPath $PSScriptRoot
if (!(Test-Path $repoRoot)) { throw "Repo root not found: $repoRoot" }

$profilesPath = Join-Path $repoRoot 'scripts\run\_profiles.ps1'
if (!(Test-Path $profilesPath)) { throw "Missing: $profilesPath" }

# Dot-source profile flags (must not self-dot-source inside _profiles.ps1)
. $profilesPath
$flags = Get-EmbeddingShiftProfileFlags -Profile $Profile

$datasetStage00 = Join-Path $repoRoot ("results\insurance\tenants\{0}\datasets\{1}\stage-00" -f $Tenant, $DatasetName)
$runsRoot       = Join-Path $repoRoot ("results\insurance\tenants\{0}\runs" -f $Tenant)

function Invoke-ConsoleEval {
  param([Parameter(Mandatory = $true)][string[]] $Args)

  Push-Location $repoRoot
  try {
    $buildArgs = @()
    if ($NoBuild) { $buildArgs += '--no-build' }

    & dotnet run @buildArgs --project .\src\EmbeddingShift.ConsoleEval\EmbeddingShift.ConsoleEval.csproj -- --tenant $Tenant @flags @Args
    if ($LASTEXITCODE -ne 0) { throw "ConsoleEval failed with exit code $LASTEXITCODE" }
  }
  finally {
    Pop-Location
  }
}

Write-Host "=== PosNeg-Only Runbook ==="
Write-Host "Tenant      : $Tenant"
Write-Host "Profile     : $Profile"
Write-Host "DatasetName : $DatasetName"
Write-Host "Seed/Size   : $Seed / $Size"
Write-Host "Stage-00    : $datasetStage00"
Write-Host "RunsRoot    : $runsRoot"
Write-Host "NoBuild     : $NoBuild"
if ($SkipGenerate) {
  Write-Host "Generate    : skipped"
} elseif (($Policies -gt 0) -or ($QueryCount -gt 0)) {
  Write-Host "Generator   : explicit (stages/policies/queries)"
  Write-Host "  Stages    : $Stages"
  Write-Host "  Policies  : $Policies"
  Write-Host "  Queries   : $QueryCount"
} else {
  Write-Host "Generator   : size-based"
}
Write-Host ""

# 0) Dataset generate (optional)
if (-not $SkipGenerate) {
  if (($Policies -gt 0) -or ($QueryCount -gt 0)) {
    if ($Policies -le 0)   { throw "Explicit generator requires -Policies > 0." }
    if ($QueryCount -le 0) { throw "Explicit generator requires -QueryCount > 0." }

    $genArgs = @(
      'domain','mini-insurance','dataset-generate', $DatasetName,
      '--stages',"$Stages",
      '--policies',"$Policies",
      '--queries',"$QueryCount",
      '--seed',"$Seed"
    )
  } else {
    $genArgs = @(
      'domain','mini-insurance','dataset-generate', $DatasetName,
      '--seed',"$Seed",
      '--size',"$Size"
    )
  }

  if ($Overwrite) { $genArgs += '--overwrite' }
  Invoke-ConsoleEval -Args $genArgs
}

# 1) Dataset root env var
$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $datasetStage00

# 2) Ingest
$refsPath    = Join-Path $datasetStage00 'policies'
$queriesPath = Join-Path $datasetStage00 'queries'
Invoke-ConsoleEval -Args @('ingest-dataset', $refsPath, $queriesPath, $datasetStage00)

# 3) Validate
Invoke-ConsoleEval -Args @('dataset-validate', $datasetStage00, '--role=all', '--require-state')

# 4) PosNeg train
Invoke-ConsoleEval -Args @('domain','mini-insurance','posneg-train', ("--mode={0}" -f $TrainMode))

# 5) PosNeg run
Invoke-ConsoleEval -Args @('domain','mini-insurance','posneg-run')

# 6) Compare
Invoke-ConsoleEval -Args @('runs-compare', ("--metric={0}" -f $Metric), '--runs-root', $runsRoot)

Write-Host ""
Write-Host "Done."
