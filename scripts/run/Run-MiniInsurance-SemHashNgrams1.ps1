param(
    [string]$Tenant = 'insurer-a',
    [int]$Seed = 1006,

    [int]$Policies = 80,
    [int]$Queries = 160,
    [int]$Stages = 3,

    [string]$EmbeddingBackend = 'sim',
    [string]$SimMode = 'deterministic',

    # Optional: override the dataset name (default: FirstLight3-$Seed)
    [string]$Dataset = '',

    # Optional: override the isolated run root. If empty, a timestamped folder under results\_scratch is used.
    [string]$Root = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. "$PSScriptRoot\..\lib\DotNet.ps1"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

if ([string]::IsNullOrWhiteSpace($Dataset)) {
    $Dataset = "FirstLight3-$Seed"
}

if ([string]::IsNullOrWhiteSpace($Root)) {
    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $Root = Join-Path $RepoRoot "results\_scratch\EmbeddingShift.MiniInsurance\$stamp"
}

New-Item -ItemType Directory -Path $Root -Force | Out-Null

# Store key values in the current process for follow-up scripts (Health, etc.).
$env:EMBEDDINGSHIFT_ROOT = $Root
$env:EMBEDDINGSHIFT_TENANT = $Tenant
$env:EMBEDDINGSHIFT_DATASET = $Dataset

Write-Host "[MiniInsurance] ROOT   = $Root"
Write-Host "[MiniInsurance] TENANT = $Tenant"
Write-Host "[MiniInsurance] DATASET= $Dataset"

# 1) Generate (tenant-scoped) dataset into the isolated root
Invoke-DotNet -Args @(
    'run','--project','src/EmbeddingShift.ConsoleEval','--',
    'domain','mini-insurance','dataset-generate', $Dataset,
    '--tenant', $Tenant,
    "--policies=$Policies",
    "--queries=$Queries",
    "--stages=$Stages",
    "--seed=$Seed",
    '--overwrite'
)

# Expected dataset root under the isolated run root
$datasetRootRun = Join-Path $Root "results\insurance\tenants\$Tenant\datasets\$Dataset\stage-00"
$datasetRootRepo = Join-Path $RepoRoot "results\insurance\tenants\$Tenant\datasets\$Dataset\stage-00"

if (Test-Path (Join-Path $datasetRootRun 'policies')) {
    $DatasetRoot = $datasetRootRun
}
elseif (Test-Path (Join-Path $datasetRootRepo 'policies')) {
    Write-Warning "Dataset was written under the repo results tree (fallback)."
    $DatasetRoot = $datasetRootRepo
}
else {
    throw "Policies directory not found. Tried:`n- $datasetRootRun`n- $datasetRootRepo"
}

$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $DatasetRoot
$env:EMBEDDINGSHIFT_DATASET_ROOT = $DatasetRoot


# Derive the tenant base and the runs root from the dataset root (works for both scratch and repo layouts).
$TenantBase = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $DatasetRoot))
$RunsRoot   = Join-Path $TenantBase 'runs'

# 2) Run the pipeline (deterministic SIM by default)
Invoke-DotNet -Args @(
    'run','--project','src/EmbeddingShift.ConsoleEval','--',
    'domain','mini-insurance','pipeline',
    '--embeddingBackend', $EmbeddingBackend,
    '--simMode', $SimMode
)

# 3) Compare baseline vs best posneg (write a report)
$DecisionMetric = 'ndcg@3'
$WinnerThreshold = 0.02

$Stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$CompareId  = "compare_{0}"  -f $Stamp
$DecisionId = "decision_{0}" -f $Stamp

DotNet-RunConsoleEval @(
    'runs-compare',
    '--runRoot', $RunsRoot,
    '--tenant', $Tenant,
    '--setA', 'baseline',
    '--setB', 'posnegBest',
    '--compareId', $CompareId,
    '--metric', $DecisionMetric,
    '--write'
)

DotNet-RunConsoleEval @(
    'runs-decide',
    '--runRoot', $RunsRoot,
    '--tenant', $Tenant,
    '--compareId', $CompareId,
    '--decisionId', $DecisionId,
    '--metric', $DecisionMetric,
    '--winnerThreshold', "$WinnerThreshold",
    '--write'
)

function Get-LatestReportPath([string]$Dir) {
    if (-not (Test-Path -LiteralPath $Dir)) { return $null }

    $f = Get-ChildItem -LiteralPath $Dir -Filter '*.md' -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $f) { return $null }
    return $f.FullName
}

$CompareDir    = Join-Path $RunsRoot '_compare'
$DecisionsDir  = Join-Path $RunsRoot '_decisions'

$ComparePath = Get-LatestReportPath -Dir $CompareDir
$DecidePath  = Get-LatestReportPath -Dir $DecisionsDir

Write-Host ""
Write-Host "Reports:"
Write-Host ("  Compare: {0}" -f ($(if ($ComparePath) { $ComparePath } else { "<none>" })))
Write-Host ("  Decide : {0}" -f ($(if ($DecidePath)  { $DecidePath  } else { "<none>" })))

# Next
Write-Host ""
Write-Host "Next:"
Write-Host "  ./scripts/runbook/40-Health.ps1"

