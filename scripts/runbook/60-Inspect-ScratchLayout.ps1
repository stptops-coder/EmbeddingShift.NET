[CmdletBinding()]
param(
  # Tenant folder name under results\insurance\tenants\<tenant>
  [string]$Tenant = $(if (-not [string]::IsNullOrWhiteSpace($env:EMBEDDINGSHIFT_TENANT)) { $env:EMBEDDINGSHIFT_TENANT } else { 'insurer-a' }),

  # Scratch scenario folder name under results\_scratch\<Scenario> (e.g. 'EmbeddingShift.Sweep', 'EmbeddingShift.MiniInsurance')
  [ValidateSet('EmbeddingShift.Sweep','EmbeddingShift.MiniInsurance','RunbookRegression')]
  [string]$Scenario = 'EmbeddingShift.Sweep',

  # Optional explicit root path (the folder that contains the 'results' subfolder).
  # If not provided, the latest folder under results\_scratch\<Scenario> is used.
  [string]$Root = '',

  # Depth of the printed tree (small on purpose to avoid noisy output).
  [int]$TreeDepth = 3,

  # Validate JSON files (ConvertFrom-Json). Can be slow if many runs exist.
  [switch]$ValidateJson,

  # Only fail on missing critical structure. Warnings are printed but do not fail unless this is set.
  [switch]$FailOnWarning
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

function Get-LatestChildDir([string]$parent) {
  if (-not (Test-Path -LiteralPath $parent)) { return $null }
  $dirs = Get-ChildItem -LiteralPath $parent -Directory -ErrorAction Stop
  if ($dirs.Count -eq 0) { return $null }
  # Folder names are yyyyMMdd_HHmmss => lexical order == chronological
  return ($dirs | Sort-Object Name -Descending | Select-Object -First 1).FullName
}

function Write-Tree([string]$path, [int]$depth) {
  if ($depth -lt 0) { return }
  $indent = ''
  $stack = @(@{ Path = $path; Depth = 0 })

  while ($stack.Count -gt 0) {
    $cur = $stack[0]
    if ($stack.Count -eq 1) { $stack = @() } else { $stack = $stack[1..($stack.Count-1)] }
    $p = $cur.Path
    $d = $cur.Depth

    $indent = ('  ' * $d)
    $name = Split-Path -Leaf $p
    Write-Host "$indent- $name"

    if ($d -ge $depth) { continue }

    $children = Get-ChildItem -LiteralPath $p -Force -ErrorAction SilentlyContinue
    $dirs = @($children | Where-Object { $_.PSIsContainer } | Sort-Object Name)
    $files = @($children | Where-Object { -not $_.PSIsContainer } | Sort-Object Name)

    foreach ($f in $files) {
      Write-Host ('  ' * ($d+1)) + "* " + $f.Name
    }
    foreach ($dir in ($dirs | Sort-Object Name -Descending)) {
      # push in reverse to keep display stable (ascending)
      $stack = @(@{ Path = $dir.FullName; Depth = ($d+1) }) + $stack
    }
  }
}

function Add-Warning([string]$msg) {
  Write-Warning $msg
  $script:WarnCount++
}

$RepoRoot = Get-RepoRoot
$ResultsRoot = Join-Path $RepoRoot 'results'
$ScratchRoot = Join-Path $ResultsRoot ('_scratch\' + $Scenario)

$WarnCount = 0

if ([string]::IsNullOrWhiteSpace($Root)) {
  $Root = Get-LatestChildDir $ScratchRoot
  if ($null -eq $Root) {
    throw "No scratch output found for scenario '$Scenario' under: $ScratchRoot"
  }
}

$Root = (Resolve-Path -LiteralPath $Root).Path
Write-Host "[Inspect] RepoRoot    = $RepoRoot"
Write-Host "[Inspect] ResultsRoot = $ResultsRoot"
Write-Host "[Inspect] Scenario    = $Scenario"
Write-Host "[Inspect] Root        = $Root"
Write-Host "[Inspect] Tenant      = $Tenant"

$base = Join-Path $Root ('results\insurance\tenants\' + $Tenant)
if (-not (Test-Path -LiteralPath $base)) {
  throw "Expected tenant base path not found: $base"
}

$datasets = Join-Path $base 'datasets'
$runs     = Join-Path $base 'runs'
$aggs     = Join-Path $base 'aggregates'

if (-not (Test-Path -LiteralPath $datasets)) { Add-Warning "Missing datasets folder: $datasets" }
if (-not (Test-Path -LiteralPath $runs))     { Add-Warning "Missing runs folder: $runs" }

# Legacy/non-tenant layout (should normally not be produced by current runbooks)
$legacyDatasets = Join-Path $Root 'results\insurance\datasets'
$legacyRunRoots = Join-Path $Root 'results\insurance\runroots'
if (Test-Path -LiteralPath $legacyDatasets) { Add-Warning "Legacy non-tenant folder exists (optional): $legacyDatasets" }
if (Test-Path -LiteralPath $legacyRunRoots) { Add-Warning "Legacy non-tenant folder exists (optional): $legacyRunRoots" }

# Summaries
if (Test-Path -LiteralPath $datasets) {
  $ds = Get-ChildItem -LiteralPath $datasets -Directory -ErrorAction SilentlyContinue
  Write-Host ("[Inspect] Datasets    = {0}" -f $ds.Count)
  foreach ($d in ($ds | Sort-Object Name)) {
    $stages = Get-ChildItem -LiteralPath $d.FullName -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like 'stage-*' }
    Write-Host ("  - {0} (stages={1})" -f $d.Name, $stages.Count)
  }
}

if (Test-Path -LiteralPath $runs) {
  $runDirs = Get-ChildItem -LiteralPath $runs -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike '_*' }
  Write-Host ("[Inspect] Runs        = {0}" -f $runDirs.Count)

  $hasActive = Test-Path -LiteralPath (Join-Path $runs '_active')
  $hasCompare = Test-Path -LiteralPath (Join-Path $runs '_compare')
  $hasDecisions = Test-Path -LiteralPath (Join-Path $runs '_decisions')
  Write-Host ("[Inspect] Side folders: active={0} compare={1} decisions={2}" -f $hasActive, $hasCompare, $hasDecisions)

  # Look for run.json presence (critical marker that a run directory is complete)
  $missingRunJson = 0
  foreach ($rd in $runDirs) {
    $runJson = Join-Path $rd.FullName 'run.json'
    if (-not (Test-Path -LiteralPath $runJson)) { $missingRunJson++ }
  }
  if ($missingRunJson -gt 0) { Add-Warning "Some run directories are missing run.json: $missingRunJson" }
}

# JSON validation (optional)
if ($ValidateJson) {
  Write-Host "[Inspect] Validating JSON (ConvertFrom-Json)..."
  $jsonFiles = Get-ChildItem -LiteralPath $base -Recurse -File -Filter '*.json' -ErrorAction SilentlyContinue
  $bad = 0
  foreach ($jf in $jsonFiles) {
    try {
      Get-Content -LiteralPath $jf.FullName -Raw -Encoding UTF8 | ConvertFrom-Json | Out-Null
    } catch {
      $bad++
      Add-Warning ("Invalid JSON: {0}" -f $jf.FullName)
    }
  }
  Write-Host ("[Inspect] JSON files  = {0}, invalid={1}" -f $jsonFiles.Count, $bad)
}

# Minimal tree for sanity (avoid huge logs)
Write-Host ""
Write-Host ("[Inspect] Tree (depth={0})" -f $TreeDepth)
Write-Tree -path $base -depth $TreeDepth

# File/size stats
$files = Get-ChildItem -LiteralPath $base -Recurse -File -ErrorAction SilentlyContinue
$totalBytes = ($files | Measure-Object -Property Length -Sum).Sum
Write-Host ""
Write-Host ("[Inspect] Files       = {0}" -f $files.Count)
Write-Host ("[Inspect] Total bytes = {0:n0}" -f $totalBytes)

# Unknown extension scan (warning only)
$allowed = @('.json','.md','.txt','.log','.bin','.csv')
$unknown = $files | Where-Object { $allowed -notcontains $_.Extension.ToLowerInvariant() } | Select-Object -First 25
if ($unknown.Count -gt 0) {
  Add-Warning ("Unknown/rare file extensions detected (showing up to 25):")
  foreach ($u in $unknown) {
    Write-Host ("  - {0}" -f $u.FullName)
  }
}

if ($WarnCount -gt 0) {
  Write-Host ""
  Write-Host ("[Inspect] WARNINGS = {0}" -f $WarnCount)
  if ($FailOnWarning) { exit 2 }
} else {
  Write-Host ""
  Write-Host "[Inspect] OK"
}

exit 0
