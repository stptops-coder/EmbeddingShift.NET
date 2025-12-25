param(
  [Parameter(Mandatory = $true)]
  [string]$RunRoot,

  [string]$Domain = "insurance",

  [switch]$WriteReport,

  [switch]$Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-FullPath([string]$p) {
  if (-not (Test-Path -LiteralPath $p)) { return $null }
  return (Resolve-Path -LiteralPath $p).Path
}

function Has-AnyFile([string]$dir) {
  if (-not (Test-Path -LiteralPath $dir)) { return $false }
  $hit = Get-ChildItem -LiteralPath $dir -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
  return ($null -ne $hit)
}

function Add-Row(
  [System.Collections.Generic.List[object]]$rows,
  [string]$item,
  [string]$status,
  [string]$path,
  [string]$note
) {
  $rows.Add([pscustomobject]@{
    Item   = $item
    Status = $status
    Path   = $path
    Note   = $note
  }) | Out-Null
}

$rr = Resolve-FullPath $RunRoot
if (-not $rr) { throw "RunRoot not found: $RunRoot" }

$domainRoot = Join-Path $rr ("results\" + $Domain)
$vectorRoot = Join-Path $rr "data\vectorstore"

$rows = New-Object System.Collections.Generic.List[object]

# Core files
$manifest = Join-Path $rr "manifest.json"
$cases    = Join-Path $rr "cases.json"
$index    = Join-Path $rr "index.json"

Add-Row $rows "manifest.json" $(if (Test-Path -LiteralPath $manifest) { "OK" } else { "MISSING" }) $manifest ""
Add-Row $rows "cases.json"    $(if (Test-Path -LiteralPath $cases)    { "OK" } else { "MISSING" }) $cases    ""
Add-Row $rows "index.json"    $(if (Test-Path -LiteralPath $index)    { "OK" } else { "MISSING" }) $index    "optional (written by Inspect-RunRoot.ps1 -WriteJsonIndex)"

# Domain root + expected contract folders (check only)
Add-Row $rows "results\<domain>" $(if (Test-Path -LiteralPath $domainRoot) { "OK" } else { "MISSING" }) $domainRoot ""

$contractFolders = @("datasets", "training", "runs", "reports", "experiments", "aggregates", "inspect")
foreach ($f in $contractFolders) {
  $p = Join-Path $domainRoot $f
  Add-Row $rows ("contract/" + $f) $(if (Test-Path -LiteralPath $p) { "OK" } else { "MISSING" }) $p ""
}

# Summary (from runroot-summarize)
$summary = Join-Path $domainRoot "reports\summary.txt"
Add-Row $rows "summary.txt" $(if (Test-Path -LiteralPath $summary) { "OK" } else { "MISSING" }) $summary "create via: domain mini-insurance runroot-summarize --runroot=<RunRoot>"

# Datasets
$datasetsRoot = Join-Path $domainRoot "datasets"
if (Test-Path -LiteralPath $datasetsRoot) {
  $datasets = @(Get-ChildItem -LiteralPath $datasetsRoot -Directory -ErrorAction SilentlyContinue)
  Add-Row $rows "datasets.count" "OK" $datasetsRoot ("count=" + $datasets.Count)

  foreach ($d in $datasets) {
    foreach ($s in 0..2) {
      $stageName = ("stage-{0:d2}" -f $s)
      $stageDir  = Join-Path $d.FullName $stageName
      $ok = (Test-Path -LiteralPath $stageDir) -and (Has-AnyFile $stageDir)

      Add-Row $rows ("dataset/" + $d.Name + "/" + $stageName) $(if ($ok) { "OK" } else { "EMPTY_OR_MISSING" }) $stageDir ""
    }
  }
}
else {
  Add-Row $rows "datasets" "MISSING" $datasetsRoot ""
}

# Training artifacts (best-effort search)
$trainingFiles = @()
if (Test-Path -LiteralPath $domainRoot) {
  $trainingFiles += @(Get-ChildItem -LiteralPath $domainRoot -Recurse -File -Filter "shift-training-result.json" -ErrorAction SilentlyContinue)
  $trainingFiles += @(Get-ChildItem -LiteralPath $domainRoot -Recurse -File -Filter "*training*result*.json" -ErrorAction SilentlyContinue)
}
$trainingFiles = @($trainingFiles | Select-Object -Unique)
Add-Row $rows "training.results" $(if ($trainingFiles.Count -gt 0) { "OK" } else { "WARN" }) $domainRoot ("found=" + $trainingFiles.Count)

# Run outputs
$runDirs = @()
if (Test-Path -LiteralPath $domainRoot) {
  $runDirs += @(
    Get-ChildItem -LiteralPath $domainRoot -Directory -ErrorAction SilentlyContinue |
      Where-Object { $_.Name -like "mini-insurance-posneg-run_*" }
  )

  $runsFolder = Join-Path $domainRoot "runs"
  if (Test-Path -LiteralPath $runsFolder) {
    $runDirs += @(Get-ChildItem -LiteralPath $runsFolder -Directory -ErrorAction SilentlyContinue)
  }
}
$runDirs = @($runDirs | Select-Object -Unique)
Add-Row $rows "runs.count" $(if ($runDirs.Count -gt 0) { "OK" } else { "WARN" }) $domainRoot ("count=" + $runDirs.Count)

# Vectorstore (informational)
if (Test-Path -LiteralPath $vectorRoot) {
  foreach ($f in @("embeddings", "runs", "shifts")) {
    $p = Join-Path $vectorRoot $f
    $has = Has-AnyFile $p
    Add-Row $rows ("vectorstore/" + $f) $(if ($has) { "OK" } else { "EMPTY" }) $p "often empty if persistence is off"
  }
}
else {
  Add-Row $rows "vectorstore" "MISSING" $vectorRoot "OK if your run doesn’t persist vectorstore"
}

# Print
Write-Host ""
Write-Host "=== RunRoot Health ==="
Write-Host ("RunRoot:  {0}" -f $rr)
Write-Host ("Domain:   {0}" -f $Domain)
Write-Host ""

$rows | Format-Table -AutoSize

# Optional report file
if ($WriteReport) {
  $outDir = Join-Path $domainRoot "reports"
  if (-not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
  }

  $outPath = Join-Path $outDir "health.txt"

  $tableText = $rows | Sort-Object Item | Format-Table -AutoSize | Out-String
  $header = @(
    "=== RunRoot Health ===",
    ("RunRoot: " + $rr),
    ("Domain : " + $Domain),
    ""
  ) -join [Environment]::NewLine

  ($header + $tableText) | Out-File -LiteralPath $outPath -Encoding utf8
  Write-Host ""
  Write-Host ("Wrote: {0}" -f $outPath)
}

# Strict mode: fail on hard misses
if ($Strict) {
  $hardMissing = $rows | Where-Object {
    $_.Status -eq "MISSING" -and $_.Item -in @("manifest.json", "cases.json", "results\<domain>")
  }
  if ($hardMissing) {
    throw "Strict: hard-missing items detected."
  }
}
