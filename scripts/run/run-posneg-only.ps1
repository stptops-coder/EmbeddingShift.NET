param(
  [Parameter(Mandatory = $true)]
  [string] $Tenant,

  [Parameter(Mandatory = $true)]
  [ValidateSet('v1','v2','v3','v4')]
  [string] $Profile,

  [int] $Seed,
  [ValidateSet('small','medium','large')]
  [string] $Size,

  [string] $DatasetName,

  [switch] $Overwrite,

  [ValidateSet('production','micro')]
  [string] $TrainMode,

  [string] $Metric
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Defaults (set here to avoid parser quirks across PS versions)
if ($PSBoundParameters.ContainsKey('Seed') -eq $false)        { $Seed = 1006 }
if ($PSBoundParameters.ContainsKey('Size') -eq $false)        { $Size = 'large' }
if ($PSBoundParameters.ContainsKey('DatasetName') -eq $false) { $DatasetName = '--seed' }
if ($PSBoundParameters.ContainsKey('TrainMode') -eq $false)   { $TrainMode = 'production' }
if ($PSBoundParameters.ContainsKey('Metric') -eq $false)      { $Metric = 'ndcg@3' }

$repoRoot = 'C:\pg\RakeX'
if (!(Test-Path $repoRoot)) { throw "Repo root not found: $repoRoot" }

$profilesPath = Join-Path $repoRoot 'scripts\run\_profiles.ps1'
if (!(Test-Path $profilesPath)) { throw "Missing: $profilesPath" }
. $profilesPath -Tenant $Tenant -Profile $Profile

$flags = Get-EmbeddingShiftProfileFlags -Profile $Profile

$datasetStage00 = Join-Path $repoRoot ("results\insurance\tenants\{0}\datasets\{1}\stage-00" -f $Tenant, $DatasetName)
$runsRoot       = Join-Path $repoRoot ("results\insurance\tenants\{0}\runs" -f $Tenant)

function Invoke-ConsoleEval {
  param([Parameter(Mandatory = $true)][string[]] $Args)

  Push-Location $repoRoot
  try {
    & dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant $Tenant @flags @Args
    if ($LASTEXITCODE -ne 0) { throw "ConsoleEval failed with exit code $LASTEXITCODE" }
  }
  finally {
    Pop-Location
  }
}

Write-Host "=== PosNeg-Only Runbook ==="
Write-Host "Tenant      : $Tenant"
Write-Host "Profile     : $Profile"
Write-Host "Seed/Size   : $Seed / $Size"
Write-Host "DatasetName : $DatasetName"
Write-Host "Stage-00    : $datasetStage00"
Write-Host "RunsRoot    : $runsRoot"
Write-Host ""

# 0) Dataset generate
$genArgs = @('domain','mini-insurance','dataset-generate','--seed',"$Seed",'--size',"$Size")
if ($Overwrite) { $genArgs += '--overwrite' }
Invoke-ConsoleEval -Args $genArgs

# 1) Dataset root env var
$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $datasetStage00

# 2) Ingest
$refs    = Join-Path $datasetStage00 'policies'
$queries = Join-Path $datasetStage00 'queries'
Invoke-ConsoleEval -Args @('ingest-dataset', $refs, $queries, $datasetStage00)

# 3) Validate (require-state only)
Invoke-ConsoleEval -Args @('dataset-validate', $datasetStage00, '--role=all', '--require-state')

# 4) PosNeg train
Invoke-ConsoleEval -Args @('domain','mini-insurance','posneg-train', ("--mode={0}" -f $TrainMode))

# 5) PosNeg run
Invoke-ConsoleEval -Args @('domain','mini-insurance','posneg-run')

# 6) Compare
Invoke-ConsoleEval -Args @('runs-compare', ("--metric={0}" -f $Metric), '--runs-root', $runsRoot)

Write-Host ""
Write-Host "Done."
