param(
  [Parameter(Mandatory=$true)]
  [string]$RunRoot,

  [string]$Domain = "insurance",

  [string]$Tenant = "",

  [switch]$WriteReport
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

# --- High-level checks
$manifest = Join-Path $RunRoot "manifest.json"
$cases    = Join-Path $RunRoot "cases.json"
$index    = Join-Path $RunRoot "index.json"

$dataVectorstore = Join-Path $RunRoot "data\\vectorstore"
$dataEmbeddings  = Join-Path $RunRoot "data\\embeddings"

$contractFolders = @(
  "datasets",
  "training",
  "runs",
  "reports",
  "experiments",
  "aggregates",
  "inspect"
)

$lines = New-Object System.Collections.Generic.List[string]

$lines.Add("=== RunRoot Health ===")
$lines.Add(("TimeUtc       : {0}" -f (Get-Date).ToUniversalTime().ToString("o")))
$lines.Add(("RunRoot       : {0}" -f $RunRoot))
$lines.Add(("Domain        : {0}" -f $Domain))
$lines.Add(("DomainRoot    : {0}" -f $domainRoot))
$lines.Add(("Tenant        : {0}" -f ($(if ($selectedTenant) { $selectedTenant } else { "<none>" }))))
$lines.Add(("ContractRoot  : {0}" -f $contractRoot))
$lines.Add("")

$lines.Add("Top-level files")
$lines.Add(("  manifest.json : {0}" -f (Test-Path $manifest)))
$lines.Add(("  cases.json    : {0}" -f (Test-Path $cases)))
$lines.Add(("  index.json    : {0}" -f (Test-Path $index)))
$lines.Add("")

$lines.Add("Data folders")
$lines.Add(("  data\\embeddings  : {0}" -f (Test-Path $dataEmbeddings)))
$lines.Add(("  data\\vectorstore : {0}" -f (Test-Path $dataVectorstore)))
$lines.Add("")

$lines.Add("Contract folders (under ContractRoot)")
foreach ($f in $contractFolders) {
  $p = Join-Path $contractRoot $f
  $lines.Add(("  {0,-11} : {1}" -f $f, (Test-Path $p)))
}
$lines.Add("")

if ((Test-Path $tenantsRoot) -and [string]::IsNullOrWhiteSpace($selectedTenant)) {
  $tenantDirs = @(Get-ChildItem -Path $tenantsRoot -Directory -ErrorAction SilentlyContinue)
  if ($tenantDirs.Count -gt 1) {
    $lines.Add("Note")
    $lines.Add("  Multiple tenants detected. Use -Tenant <name> to target a specific tenant.")
    $lines.Add(("  Tenants: {0}" -f ($tenantDirs.Name -join ", ")))
    $lines.Add("")
  }
}

# Print
$lines | ForEach-Object { Write-Host $_ }

if ($WriteReport) {
  $reportsDir = Join-Path $contractRoot "reports"
  if (-not (Test-Path $reportsDir)) {
    New-Item -ItemType Directory -Path $reportsDir -Force | Out-Null
  }

  $out = Join-Path $reportsDir "health.txt"
  $lines | Out-File -LiteralPath $out -Encoding utf8
  Write-Host ""
  Write-Host ("Wrote: {0}" -f $out)
}
