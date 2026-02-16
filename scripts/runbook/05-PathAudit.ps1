param(
  [string]$RepoRoot = "",
  [string]$Tenant = "",
  [string]$Layout = "",
  [switch]$ListDeep
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot {
  param([string]$Hint)

  if ($Hint -and (Test-Path -LiteralPath $Hint)) { return (Resolve-Path -LiteralPath $Hint).Path }

  # 1) Git root if available
  try {
    $gitRoot = (git rev-parse --show-toplevel 2>$null)
    if ($gitRoot -and (Test-Path -LiteralPath $gitRoot)) { return (Resolve-Path -LiteralPath $gitRoot).Path }
  } catch { }

  # 2) Walk up from script location until we find src/ and scripts/
  $p = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
  while ($p -and (Test-Path -LiteralPath $p)) {
    if ((Test-Path (Join-Path $p "src")) -and (Test-Path (Join-Path $p "scripts"))) { return $p }
    $parent = Split-Path -Path $p -Parent
    if ($parent -eq $p) { break }
    $p = $parent
  }

  throw "Could not resolve RepoRoot. Please pass -RepoRoot <path>."
}

function Print-KV {
  param([string]$K, [string]$V)
  $v2 = if ($V) { $V } else { "<empty>" }
  Write-Host ("{0} = {1}" -f $K, $v2)
}

function List-DirSafe {
  param([string]$Path, [int]$Depth = 1)

  if (-not (Test-Path -LiteralPath $Path)) {
    Write-Host ("  (missing) {0}" -f $Path)
    return
  }

  Write-Host ("  {0}" -f $Path)
  if ($Depth -le 0) { return }

  Get-ChildItem -LiteralPath $Path -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name |
    ForEach-Object {
      Write-Host ("    [{0}]  {1}" -f $_.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"), $_.Name)
    }
}

$root = Resolve-RepoRoot -Hint $RepoRoot
$results = Join-Path $root "results"

Write-Host ""
Write-Host "=== EmbeddingShift Path Audit ==="
Write-Host ("Time      : {0}" -f (Get-Date).ToString("yyyy-MM-dd HH:mm:ss"))
Write-Host ("RepoRoot  : {0}" -f $root)
Write-Host ("Results   : {0}" -f $results)
Write-Host ""

Write-Host "=== Relevant environment variables (Process/User/Machine view) ==="
# We print Process scope (what matters for scripts)
$vars = @(
  "EMBEDDINGSHIFT_ROOT",
  "EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT",
  "EMBEDDINGSHIFT_TENANT",
  "EMBEDDINGSHIFT_PROVIDER",
  "EMBEDDINGSHIFT_BACKEND",
  "EMBEDDINGSHIFT_SIM_MODE",
  "EMBEDDINGSHIFT_SIM_ALGO"
)

foreach ($v in $vars) {
  $p = [Environment]::GetEnvironmentVariable($v, "Process")
  Print-KV -K ("Process:{0}" -f $v) -V $p
}
Write-Host ""

Write-Host "=== Derived key paths ==="
$activeRunRoot = [Environment]::GetEnvironmentVariable("EMBEDDINGSHIFT_ROOT", "Process")
if ($activeRunRoot) {
  try { $activeRunRoot = (Resolve-Path -LiteralPath $activeRunRoot).Path } catch { }
}
$dsRoot = [Environment]::GetEnvironmentVariable("EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT", "Process")
if ($dsRoot) {
  # Allow relative ds-root (common in docs)
  if (-not ([System.IO.Path]::IsPathRooted($dsRoot))) {
    $dsRoot = Join-Path $root $dsRoot
  }
  try { $dsRoot = (Resolve-Path -LiteralPath $dsRoot).Path } catch { }
}

$tenant2 = $Tenant
if (-not $tenant2) {
  $tenant2 = [Environment]::GetEnvironmentVariable("EMBEDDINGSHIFT_TENANT", "Process")
}

Print-KV "ActiveRunRoot" $activeRunRoot
Print-KV "DatasetRoot"  $dsRoot
Print-KV "Tenant(arg/env)" $tenant2
 
$layout = $Layout
if (-not $layout) {
  $layout = [Environment]::GetEnvironmentVariable("EMBEDDINGSHIFT_LAYOUT", "Process")
}
if (-not $layout) {
  $layout = 'tenant'
}
$layout = $layout.ToLowerInvariant()
Print-KV "Layout(arg/env)" $layout
Write-Host ""

Write-Host "=== Existence checks ==="
Write-Host ("RepoRoot exists  : {0}" -f (Test-Path -LiteralPath $root))
Write-Host ("Results exists   : {0}" -f (Test-Path -LiteralPath $results))
Write-Host ("RunRoot exists   : {0}" -f ($(if ($activeRunRoot) { Test-Path -LiteralPath $activeRunRoot } else { $false })))
Write-Host ("Dataset exists   : {0}" -f ($(if ($dsRoot) { Test-Path -LiteralPath $dsRoot } else { $false })))
Write-Host ""

Write-Host "=== High-level folder map ==="
Write-Host "Results top:"
List-DirSafe -Path $results -Depth 1
Write-Host ""

# Common locations we’ve used in this repo
# Common locations we’ve used in this repo
$pathsToCheck = @(
  (Join-Path $results "_scratch"),
  (Join-Path $results "insurance"),
  (Join-Path $results "insurance\tenants")
)

if ($layout -ne 'tenant') {
  # Legacy/non-tenant layout folders
  $pathsToCheck += (Join-Path $results "insurance\datasets")
  $pathsToCheck += (Join-Path $results "insurance\runroots")
} else {
  Write-Host "Key folders (tenant layout): insurance\datasets and insurance\runroots are legacy/optional."
}

Write-Host "Key folders:"
foreach ($p in $pathsToCheck) {
  List-DirSafe -Path $p -Depth 1
}
Write-Host ""

if ($tenant2) {
  Write-Host "Tenant-specific (if present):"
  $tenantPath = Join-Path $results ("insurance\tenants\{0}" -f $tenant2)
  List-DirSafe -Path $tenantPath -Depth 2
  Write-Host ""

# Also show the run roots inside the tenant (keeps the main map compact but makes _best/_active visible).
$runsDir = Join-Path $tenantPath 'runs'
if (Test-Path -LiteralPath $runsDir) {
  Write-Host "Runs (tenant):"
  List-DirSafe -Path $runsDir -Indent '  ' -Depth 2

  $bestDir = Join-Path $runsDir '_best'
  $activeDir = Join-Path $runsDir '_active'
  $historyDir = Join-Path $activeDir 'history'

  Write-Host ""
  Write-Host "Run pointers:"
  Write-Host ("  _best   : {0}" -f (Test-Path -LiteralPath $bestDir))
  Write-Host ("  _active : {0}" -f (Test-Path -LiteralPath $activeDir))
  Write-Host ("  history : {0}" -f (Test-Path -LiteralPath $historyDir))

  if (Test-Path -LiteralPath $bestDir) {
    $bestFiles = Get-ChildItem -LiteralPath $bestDir -File -Filter 'best_*.json' -ErrorAction SilentlyContinue
    if ($bestFiles) {
      Write-Host "  best files:"
      foreach ($f in $bestFiles) { Write-Host ("    - {0}" -f $f.Name) }
    }
  }

  if (Test-Path -LiteralPath $activeDir) {
    $activeFiles = Get-ChildItem -LiteralPath $activeDir -File -Filter 'active_*.json' -ErrorAction SilentlyContinue
    if ($activeFiles) {
      Write-Host "  active files:"
      foreach ($f in $activeFiles) { Write-Host ("    - {0}" -f $f.Name) }
    }
  }

  Write-Host ""
}
}

if ($activeRunRoot) {
  Write-Host "Active RunRoot snapshot:"
  List-DirSafe -Path $activeRunRoot -Depth 2
  Write-Host ""
}

if ($ListDeep) {
  Write-Host "=== DEEP LIST (may be noisy) ==="
  foreach ($p in $pathsToCheck) {
    if (Test-Path -LiteralPath $p) {
      Write-Host ""
      Write-Host ("[Deep] {0}" -f $p)
      Get-ChildItem -LiteralPath $p -Recurse -Force -ErrorAction SilentlyContinue |
        Select-Object FullName, Length, LastWriteTime |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 200 |
        Format-Table -AutoSize
    }
  }
}

Write-Host ""
Write-Host "=== Done. ==="