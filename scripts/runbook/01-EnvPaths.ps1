param(
  [string]$Tenant = "",
  [ValidateSet("scratch","tenant")][string]$Layout = "scratch",
  [string]$Domain = "insurance",
  [string]$Scenario = "EmbeddingShift.MiniInsurance",
  [string]$DatasetRelative = "samples\insurance",
  [switch]$Force,
  [switch]$ClearOptional,
  [switch]$CreateFolders
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
  # scripts/runbook -> scripts -> repo root
  return (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function New-Timestamp {
  return (Get-Date).ToString('yyyyMMdd_HHmmss')
}

function Ensure-Dir([string]$Path) {
  if (-not (Test-Path -LiteralPath $Path)) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
  }
}

$repoRoot = Get-RepoRoot
$resultsRoot = Join-Path $repoRoot 'results'
$datasetRoot = Join-Path $repoRoot $DatasetRelative

if ($CreateFolders) {
  Ensure-Dir $resultsRoot
  Ensure-Dir $datasetRoot
}

$ts = New-Timestamp

if ($Layout -eq 'tenant' -and -not [string]::IsNullOrWhiteSpace($Tenant)) {
  $activeRunRoot = Join-Path $resultsRoot (Join-Path $Domain (Join-Path 'tenants' (Join-Path $Tenant (Join-Path '_scratch' (Join-Path $Scenario $ts)))))
}
else {
  $activeRunRoot = Join-Path $resultsRoot (Join-Path '_scratch' (Join-Path $Scenario $ts))
}

if ($CreateFolders) {
  Ensure-Dir $activeRunRoot
}

# Always set process-level roots for this session.
$env:EMBEDDINGSHIFT_RESULTS_ROOT = $resultsRoot
$env:EMBEDDINGSHIFT_ROOT = $activeRunRoot

if ($Force -or [string]::IsNullOrWhiteSpace($env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT)) {
  $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $datasetRoot
}

if ($Force -or $Layout -eq 'tenant') {
  # Keep the environment in sync with the CLI argument (prevents "stale tenant" drift).
  $env:EMBEDDINGSHIFT_TENANT = $Tenant
}

if ($ClearOptional) {
  $env:EMBEDDINGSHIFT_PROVIDER = ""
  $env:EMBEDDINGSHIFT_BACKEND = ""
  $env:EMBEDDINGSHIFT_SIM_MODE = ""
  $env:EMBEDDINGSHIFT_SIM_ALGO = ""
}

# Emit a compact summary (copy-safe).
[PSCustomObject]@{
  RepoRoot      = $repoRoot
  ResultsRoot   = $resultsRoot
  ActiveRunRoot = $activeRunRoot
  DatasetRoot   = $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT
  Tenant        = $env:EMBEDDINGSHIFT_TENANT
  Layout        = $Layout
  Scenario      = $Scenario
}
