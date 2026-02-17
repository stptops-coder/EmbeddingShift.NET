# EmbeddingShift Path Audit (Runbook)
# Purpose: diagnose path/layout settings used by runbooks & CLI.
# Notes:
# - Hardened script: StrictMode + Stop on errors.
# - Must never crash when env vars are absent.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
  # Note: In PowerShell, $MyInvocation inside a function refers to the function invocation,
  # not the script file. $PSScriptRoot / $PSCommandPath are the robust choices.
  $scriptDir = $PSScriptRoot

  if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptPath = $PSCommandPath
    if (-not [string]::IsNullOrWhiteSpace($scriptPath)) {
      $scriptDir = Split-Path -Parent $scriptPath
    }
  }

  if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    throw "[PathAudit] Failed to resolve RepoRoot from script path."
  }

  $repo = Resolve-Path (Join-Path $scriptDir '..\..')
  return $repo.Path
}


function Format-Empty([string]$value) {
  if ([string]::IsNullOrWhiteSpace($value)) { return '<empty>' }
  return $value
}

function Read-Env([string]$name, [EnvironmentVariableTarget]$target) {
  # Returns $null if not set.
  try { return [Environment]::GetEnvironmentVariable($name, $target) } catch { return $null }
}

function Write-EnvTriple([string]$name) {
  $p = Read-Env $name ([EnvironmentVariableTarget]::Process)
  $u = Read-Env $name ([EnvironmentVariableTarget]::User)
  $m = Read-Env $name ([EnvironmentVariableTarget]::Machine)
  Write-Host ("Process:{0} = {1}" -f $name, (Format-Empty $p))
  Write-Host ("User   :{0} = {1}" -f $name, (Format-Empty $u))
  Write-Host ("Machine:{0} = {1}" -f $name, (Format-Empty $m))
}

$repoRoot = Get-RepoRoot
$repoResultsRoot = Join-Path $repoRoot 'results'

$activeRunRoot = $env:EMBEDDINGSHIFT_ROOT
$activeResults = if (-not [string]::IsNullOrWhiteSpace($activeRunRoot)) {
  Join-Path $activeRunRoot 'results'
} elseif (-not [string]::IsNullOrWhiteSpace($env:EMBEDDINGSHIFT_DATA_ROOT)) {
  $env:EMBEDDINGSHIFT_DATA_ROOT
} elseif (-not [string]::IsNullOrWhiteSpace($env:EMBEDDINGSHIFT_RESULTS_ROOT)) {
  $env:EMBEDDINGSHIFT_RESULTS_ROOT
} else {
  $repoResultsRoot
}

$datasetRoot = if (-not [string]::IsNullOrWhiteSpace($env:EMBEDDINGSHIFT_DATASET_ROOT)) {
  $env:EMBEDDINGSHIFT_DATASET_ROOT
} elseif (-not [string]::IsNullOrWhiteSpace($env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT)) {
  $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT
} else {
  $null
}

$tenant = $env:EMBEDDINGSHIFT_TENANT
$layout = if (-not [string]::IsNullOrWhiteSpace($env:EMBEDDINGSHIFT_LAYOUT)) { $env:EMBEDDINGSHIFT_LAYOUT } else { 'tenant' }

$time = Get-Date
Write-Host ""
Write-Host "=== EmbeddingShift Path Audit ==="
Write-Host ("Time            : {0}" -f $time.ToString('yyyy-MM-dd HH:mm:ss'))
Write-Host ("RepoRoot        : {0}" -f $repoRoot)
Write-Host ("RepoResultsRoot : {0}" -f $repoResultsRoot)
Write-Host ("ActiveRunRoot   : {0}" -f (Format-Empty $activeRunRoot))
Write-Host ("ActiveResults   : {0}" -f (Format-Empty $activeResults))

Write-Host ""
Write-Host "=== Relevant environment variables (Process/User/Machine view) ==="
$vars = @(
  'EMBEDDINGSHIFT_ROOT',
  'EMBEDDINGSHIFT_DATA_ROOT',
  'EMBEDDINGSHIFT_RESULTS_ROOT',
  'EMBEDDINGSHIFT_RESULTS_DOMAIN',
  'EMBEDDINGSHIFT_DATASET_ROOT',
  'EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT',
  'EMBEDDINGSHIFT_TENANT',
  'EMBEDDINGSHIFT_LAYOUT',
  'EMBEDDINGSHIFT_PROVIDER',
  'EMBEDDINGSHIFT_BACKEND',
  'EMBEDDINGSHIFT_SIM_MODE',
  'EMBEDDINGSHIFT_SIM_ALGO'
)
foreach ($v in $vars) {
  Write-EnvTriple $v
  Write-Host ""
}

Write-Host "=== Derived key paths ==="
Write-Host ("DatasetRoot      = {0}" -f (Format-Empty $datasetRoot))
Write-Host ("Tenant(arg/env)  = {0}" -f (Format-Empty $tenant))
Write-Host ("Layout(arg/env)  = {0}" -f (Format-Empty $layout))

Write-Host ""
Write-Host "=== Existence checks ==="
Write-Host ("RepoRoot exists  : {0}" -f (Test-Path $repoRoot))
Write-Host ("Results exists   : {0}" -f (Test-Path $repoResultsRoot))
Write-Host ("RunRoot exists   : {0}" -f ((-not [string]::IsNullOrWhiteSpace($activeRunRoot)) -and (Test-Path $activeRunRoot)))
Write-Host ("ActiveResults ok : {0}" -f ((-not [string]::IsNullOrWhiteSpace($activeResults)) -and (Test-Path $activeResults)))
Write-Host ("Dataset exists   : {0}" -f ((-not [string]::IsNullOrWhiteSpace($datasetRoot)) -and (Test-Path $datasetRoot)))

Write-Host ""
Write-Host "=== High-level folder map ==="
if (Test-Path $activeResults) {
  Write-Host "ActiveResults top:"
  Get-ChildItem $activeResults -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name |
    ForEach-Object { Write-Host ("[d] {0}" -f $_.Name) }
} else {
  Write-Host "ActiveResults top: <missing>"
}

Write-Host ""
Write-Host "Key folders (tenant layout): <domain>\\tenants\\<tenant>\\datasets and <domain>\\tenants\\<tenant>\\runs"
Write-Host "Key folders (legacy): <domain>\\datasets and <domain>\\runroots"
Write-Host ""
Write-Host "=== Done. ==="
