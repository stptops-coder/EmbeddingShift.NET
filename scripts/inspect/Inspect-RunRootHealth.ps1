param(
  [Parameter(Mandatory = $true)]
  [string] $RunRoot,

  # Optional. If omitted, the script will auto-detect the single domain under <RunRoot>\results.
  [string] $Domain,

  # Optional. If omitted and layout=tenant, the script will auto-detect the single tenant under <Domain>\tenants.
  [string] $Tenant,

  # Optional. If omitted, the script will auto-detect: 'tenant' if <Domain>\tenants exists, otherwise 'domain'.
  [ValidateSet('tenant', 'domain')]
  [string] $Layout,

  [switch] $WriteReport,
  [switch] $WriteIndex
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Ensure-Directory {
  param([Parameter(Mandatory = $true)][string] $Path)
  if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
  }
}

function Get-SingleChildDirectoryNameOrThrow {
  param(
    [Parameter(Mandatory = $true)][string] $Parent,
    [Parameter(Mandatory = $true)][string] $Label
  )

  if (-not (Test-Path -LiteralPath $Parent -PathType Container)) {
    throw ("{0} root not found: {1}" -f $Label, $Parent)
  }

  $names = @(Get-ChildItem -LiteralPath $Parent -Directory | Select-Object -ExpandProperty Name)

  if ($names.Count -eq 0) {
    throw ("Cannot auto-detect {0}: no directories found under {1}" -f $Label, $Parent)
  }

  if ($names.Count -gt 1) {
    $list = ($names | Sort-Object) -join ', '
    throw ("Cannot auto-detect {0}: multiple directories found under {1}: {2}. Please pass -{0} <name>." -f $Label, $Parent, $list)
  }

  return $names[0]
}

# Validate first (avoid Resolve-Path throwing a noisy error).
if (-not (Test-Path -LiteralPath $RunRoot -PathType Container)) {
  throw "RunRoot not found: $RunRoot"
}

$runRootFull = (Resolve-Path -LiteralPath $RunRoot).Path

$resultsRoot = Join-Path $runRootFull 'results'
if (-not (Test-Path -LiteralPath $resultsRoot -PathType Container)) {
  throw "Results folder not found: $resultsRoot"
}

if ([string]::IsNullOrWhiteSpace($Domain)) {
  $Domain = Get-SingleChildDirectoryNameOrThrow -Parent $resultsRoot -Label 'Domain'
}

$domainRoot = Join-Path $resultsRoot $Domain
if (-not (Test-Path -LiteralPath $domainRoot -PathType Container)) {
  throw "Domain folder not found: $domainRoot"
}

if ([string]::IsNullOrWhiteSpace($Layout)) {
  $tenantsCandidate = Join-Path $domainRoot 'tenants'
  if (Test-Path -LiteralPath $tenantsCandidate -PathType Container) {
    $Layout = 'tenant'
  }
  else {
    $Layout = 'domain'
  }
}

$tenantsRoot  = $null
$tenantRoot   = $null
$contractRoot = $null

if ($Layout -eq 'tenant') {
  $tenantsRoot = Join-Path $domainRoot 'tenants'
  if (-not (Test-Path -LiteralPath $tenantsRoot -PathType Container)) {
    throw "Expected tenant layout but 'tenants' folder not found: $tenantsRoot"
  }

  if ([string]::IsNullOrWhiteSpace($Tenant)) {
    $Tenant = Get-SingleChildDirectoryNameOrThrow -Parent $tenantsRoot -Label 'Tenant'
  }

  $tenantRoot = Join-Path $tenantsRoot $Tenant
  if (-not (Test-Path -LiteralPath $tenantRoot -PathType Container)) {
    throw "Tenant folder not found: $tenantRoot"
  }

  $contractRoot = $tenantRoot
}
else {
  $contractRoot = $domainRoot
}

$reportsRoot = Join-Path $contractRoot 'reports'
if ($WriteReport) {
  Ensure-Directory -Path $reportsRoot
}

# Optional top-level artifacts (reserved for future tooling).
$manifestOk = Test-Path (Join-Path $runRootFull 'manifest.json')
$casesOk    = Test-Path (Join-Path $runRootFull 'cases.json')
$indexOk    = Test-Path (Join-Path $runRootFull 'index.json')

$dataEmbeddingsOk = Test-Path (Join-Path $runRootFull 'data\embeddings')

# Common contract folders
$datasetsOk    = Test-Path (Join-Path $contractRoot 'datasets')
$runsOk        = Test-Path (Join-Path $contractRoot 'runs')
$trainingOk    = Test-Path (Join-Path $contractRoot 'training')
$aggregatesOk  = Test-Path (Join-Path $contractRoot 'aggregates')
$reportsOk     = Test-Path $reportsRoot
$experimentsOk = Test-Path (Join-Path $contractRoot 'experiments')
$inspectOk     = Test-Path (Join-Path $contractRoot 'inspect')

if ($Layout -eq 'tenant') {
  $layoutInfo = "tenant ($Tenant)"
}
else {
  $layoutInfo = 'domain'
}

Write-Host "[RunRootHealth] RunRoot   : $runRootFull"
Write-Host "[RunRootHealth] Results   : $resultsRoot"
Write-Host "[RunRootHealth] Domain    : $Domain"
Write-Host "[RunRootHealth] Layout    : $layoutInfo"
Write-Host "[RunRootHealth] Contract  : $contractRoot"

Write-Host "[RunRootHealth] manifest  : $([string]$manifestOk)"
Write-Host "[RunRootHealth] cases     : $([string]$casesOk)"
Write-Host "[RunRootHealth] index     : $([string]$indexOk)"
Write-Host "[RunRootHealth] embeddings: $([string]$dataEmbeddingsOk)"

Write-Host "[RunRootHealth] datasets  : $([string]$datasetsOk)"
Write-Host "[RunRootHealth] runs      : $([string]$runsOk)"
Write-Host "[RunRootHealth] training  : $([string]$trainingOk)"
Write-Host "[RunRootHealth] aggregates: $([string]$aggregatesOk)"
Write-Host "[RunRootHealth] reports   : $([string]$reportsOk)"
Write-Host "[RunRootHealth] experiments: $([string]$experimentsOk)"
Write-Host "[RunRootHealth] inspect   : $([string]$inspectOk)"

if ($WriteReport) {
  $healthPath = Join-Path $reportsRoot 'health.txt'

  $lines = @()
  $lines += "RunRoot    : $runRootFull"
  $lines += "Results    : $resultsRoot"
  $lines += "Domain     : $Domain"
  $lines += "Layout     : $layoutInfo"
  $lines += "Contract   : $contractRoot"

  if ($Layout -eq 'tenant') {
    $lines += "Tenants    : $tenantsRoot"
    $lines += "TenantRoot : $tenantRoot"
  }

  $lines += "manifest   : $manifestOk"
  $lines += "cases      : $casesOk"
  $lines += "index      : $indexOk"
  $lines += "embeddings : $dataEmbeddingsOk"

  $lines += "datasets   : $datasetsOk"
  $lines += "runs       : $runsOk"
  $lines += "training   : $trainingOk"
  $lines += "aggregates : $aggregatesOk"
  $lines += "reports    : $reportsOk"
  $lines += "experiments: $experimentsOk"
  $lines += "inspect    : $inspectOk"

  [System.IO.File]::WriteAllLines($healthPath, $lines, [System.Text.Encoding]::UTF8)
  Write-Host "[RunRootHealth] Wrote: $healthPath"
}

if ($WriteIndex) {
  $indexPath = Join-Path $runRootFull 'index.json'

  $obj = [ordered]@{
    runRoot      = $runRootFull
    resultsRoot  = $resultsRoot
    domain       = $Domain
    layout       = $Layout
    tenant       = $Tenant
    contractRoot = $contractRoot
    reportsRoot  = $reportsRoot
    checks = [ordered]@{
      manifest    = $manifestOk
      cases       = $casesOk
      index       = $indexOk
      embeddings  = $dataEmbeddingsOk
      datasets    = $datasetsOk
      runs        = $runsOk
      training    = $trainingOk
      aggregates  = $aggregatesOk
      reports     = $reportsOk
      experiments = $experimentsOk
      inspect     = $inspectOk
    }
  }

  $json = $obj | ConvertTo-Json -Depth 8
  [System.IO.File]::WriteAllText($indexPath, $json + [Environment]::NewLine, [System.Text.Encoding]::UTF8)
  Write-Host "[RunRootHealth] Wrote: $indexPath"
}
