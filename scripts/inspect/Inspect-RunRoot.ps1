param(
  [Parameter(Mandatory=$true)]
  [string]$RunRoot,

  [string]$Domain = "insurance",

  [int]$MaxFilesPerBucket = 40,

  [switch]$WriteJsonIndex
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RelPath([string]$base, [string]$path) {
  $b = (Resolve-Path $base).Path.TrimEnd('\')
  $p = (Resolve-Path $path).Path
  if ($p.StartsWith($b, [System.StringComparison]::OrdinalIgnoreCase)) {
    return $p.Substring($b.Length).TrimStart('\')
  }
  return $p
}

function Print-Header([string]$title) {
  Write-Host ""
  Write-Host "=== $title ==="
}

function List-Files([string]$dir, [string[]]$patterns, [int]$max) {
  if (-not (Test-Path $dir)) { return @() }

  $items = New-Object System.Collections.Generic.List[object]
  foreach ($pat in $patterns) {
    Get-ChildItem -Path $dir -Recurse -File -Filter $pat -ErrorAction SilentlyContinue |
      ForEach-Object { $items.Add($_) }
  }

  $items `
    | Sort-Object LastWriteTime -Descending `
    | Select-Object -First $max `
    | ForEach-Object {
        [PSCustomObject]@{
          RelPath = (Get-RelPath $RunRoot $_.FullName)
          Name    = $_.Name
          SizeKB  = [Math]::Round($_.Length / 1024.0, 1)
          Time    = $_.LastWriteTime
        }
      }
}

function Count-Files([string]$dir) {
  if (-not (Test-Path $dir)) { return 0 }
  return (Get-ChildItem -Path $dir -Recurse -File | Measure-Object).Count
}

# --- Normalize inputs
if (-not (Test-Path $RunRoot)) { throw "RunRoot not found: $RunRoot" }
$RunRoot = (Resolve-Path $RunRoot).Path

# --- Common layout in your runs:
$ResultsRoot = Join-Path $RunRoot "results\$Domain"

Print-Header "RunRoot"
Get-ChildItem $RunRoot | Select-Object Name, LastWriteTime, Mode | Format-Table -AutoSize

if (Test-Path $ResultsRoot) {
  Print-Header "ResultsRoot"
  Write-Host ("ResultsRoot = {0}" -f $ResultsRoot)
} else {
  Print-Header "ResultsRoot"
  Write-Host "WARNING: results\$Domain not found under RunRoot."
  Write-Host "I will still index what I can."
  $ResultsRoot = $RunRoot
}

# Buckets we care about
$buckets = @(
  @{ Name="MANIFEST"; Dir=$RunRoot; Patterns=@("manifest.json","cases.json","*.md","*.txt"); },
  @{ Name="DATASETS"; Dir=(Join-Path $ResultsRoot "datasets"); Patterns=@("*.json"); },
  @{ Name="TRAINING"; Dir=$ResultsRoot; Patterns=@("shift-training-result.json","*training*.json","*delta*.json"); },
  @{ Name="RUNS"; Dir=$ResultsRoot; Patterns=@("*posneg-run*","*run*.json","metrics.json","*.json"); },
  @{ Name="REPORTS"; Dir=(Join-Path $ResultsRoot "reports"); Patterns=@("*.json","*.md","*.txt"); },
  @{ Name="EXPERIMENTS"; Dir=(Join-Path $ResultsRoot "experiments"); Patterns=@("*.json","*.md","*.txt"); }
)

$index = [ordered]@{
  runRoot = $RunRoot
  resultsRoot = $ResultsRoot
  generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
  buckets = @()
}

foreach ($b in $buckets) {
  $dir = $b.Dir
  $name = $b.Name

  Print-Header $name
  if (-not (Test-Path $dir)) {
    Write-Host ("(missing) {0}" -f $dir)
    $index.buckets += [ordered]@{
      name = $name
      dir  = $dir
      fileCount = 0
      files = @()
    }
    continue
  }

  $count = Count-Files $dir
  Write-Host ("{0}" -f $dir)
  Write-Host ("files = {0}" -f $count)

$files = @(List-Files $dir $b.Patterns $MaxFilesPerBucket)
if ($files.Count -eq 0) {
  Write-Host "(no matching files for patterns: $($b.Patterns -join ', '))"
} else {
  $files | Format-Table -AutoSize
}

  $index.buckets += [ordered]@{
    name = $name
    dir  = $dir
    fileCount = $count
    files = $files
    patterns = $b.Patterns
  }
}

if ($WriteJsonIndex) {
  $out = Join-Path $RunRoot "index.json"
  ($index | ConvertTo-Json -Depth 6) | Out-File -LiteralPath $out -Encoding utf8
  Write-Host ""
  Write-Host ("Wrote: {0}" -f $out)
}
