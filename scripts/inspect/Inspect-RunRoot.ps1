param(
  [Parameter(Mandatory=$true)]
  [string]$RunRoot,

  [string]$Domain = "insurance",

  [string]$Tenant = "",

  [int]$MaxFilesPerBucket = 40,

  [switch]$WriteJsonIndex
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RelPath([string]$base, [string]$path) {
  $b = (Resolve-Path $base).Path.TrimEnd('\\')
  $p = (Resolve-Path $path).Path
  if ($p.StartsWith($b, [System.StringComparison]::OrdinalIgnoreCase)) {
    return $p.Substring($b.Length).TrimStart('\\')
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

  $items |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First $max |
    ForEach-Object {
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

$domainRoot = Join-Path (Join-Path $RunRoot "results") $Domain
$tenantsRoot = Join-Path $domainRoot "tenants"

$contractRoot = $domainRoot
$selectedTenant = ""

if (Test-Path $tenantsRoot) {
  if (-not [string]::IsNullOrWhiteSpace($Tenant)) {
    $candidate = Join-Path $tenantsRoot $Tenant
    if (Test-Path $candidate) {
      $contractRoot = $candidate
      $selectedTenant = $Tenant
    }
  } else {
    $dirs = @(Get-ChildItem -Path $tenantsRoot -Directory -ErrorAction SilentlyContinue)
    if ($dirs.Count -eq 1) {
      $contractRoot = $dirs[0].FullName
      $selectedTenant = $dirs[0].Name
    }
  }
}

Print-Header "RunRoot"
Get-ChildItem $RunRoot | Select-Object Name, LastWriteTime, Mode | Format-Table -AutoSize

Print-Header "Results"
Write-Host ("DomainRoot   = {0}" -f $domainRoot)
Write-Host ("Tenant       = {0}" -f ($(if ($selectedTenant) { $selectedTenant } else { "<none>" })))
Write-Host ("ContractRoot = {0}" -f $contractRoot)

if ((Test-Path $tenantsRoot) -and [string]::IsNullOrWhiteSpace($selectedTenant)) {
  $tenantDirs = @(Get-ChildItem -Path $tenantsRoot -Directory -ErrorAction SilentlyContinue)
  if ($tenantDirs.Count -gt 1) {
    Write-Host "NOTE: Multiple tenants detected. Use -Tenant <name> to target a specific tenant." 
    Write-Host ("Tenants: {0}" -f ($tenantDirs.Name -join ", "))
  }
}

# Buckets we care about
$buckets = @(
  @{ Name="MANIFEST";    Dir=$RunRoot;                    Patterns=@("manifest.json","cases.json","index.json","*.md","*.txt"); },
  @{ Name="DATA";        Dir=(Join-Path $RunRoot "data"); Patterns=@("*.json","*.bin","*.md","*.txt"); },
  @{ Name="DATASETS";    Dir=(Join-Path $contractRoot "datasets"); Patterns=@("*.json","*.md","*.txt"); },
  @{ Name="TRAINING";    Dir=(Join-Path $contractRoot "training"); Patterns=@("shift-training-result.json","*training*.json","*delta*.json","*.md","*.txt"); },
  @{ Name="AGGREGATES";  Dir=(Join-Path $contractRoot "aggregates"); Patterns=@("metrics-aggregate.json","metrics-aggregate.md","*.json","*.md","*.txt"); },
  @{ Name="RUNS";        Dir=(Join-Path $contractRoot "runs"); Patterns=@("run.json","report.md","*.json","*.md","*.txt"); },
  @{ Name="REPORTS";     Dir=(Join-Path $contractRoot "reports"); Patterns=@("*.json","*.md","*.txt"); },
  @{ Name="EXPERIMENTS"; Dir=(Join-Path $contractRoot "experiments"); Patterns=@("*.json","*.md","*.txt"); },
  @{ Name="INSPECT";     Dir=(Join-Path $contractRoot "inspect"); Patterns=@("*.json","*.md","*.txt"); }
)

$index = [ordered]@{
  runRoot       = $RunRoot
  domainRoot    = $domainRoot
  contractRoot  = $contractRoot
  domain        = $Domain
  tenant        = ($(if ($selectedTenant) { $selectedTenant } else { "" }))
  generatedUtc  = (Get-Date).ToUniversalTime().ToString("o")
  buckets       = @()
}

foreach ($b in $buckets) {
  $dir = $b.Dir
  $name = $b.Name

  Print-Header $name
  if (-not (Test-Path $dir)) {
    Write-Host ("(missing) {0}" -f $dir)
    $index.buckets += [ordered]@{ name=$name; dir=$dir; fileCount=0; files=@(); patterns=$b.Patterns }
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

  $index.buckets += [ordered]@{ name=$name; dir=$dir; fileCount=$count; files=$files; patterns=$b.Patterns }
}

if ($WriteJsonIndex) {
  $out = Join-Path $RunRoot "index.json"
  ($index | ConvertTo-Json -Depth 7) | Out-File -LiteralPath $out -Encoding utf8
  Write-Host ""
  Write-Host ("Wrote: {0}" -f $out)
}
