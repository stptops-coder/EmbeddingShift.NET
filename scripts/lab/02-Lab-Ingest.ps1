<#
Lab helper: run the canonical MiniInsurance ingest/inspect/validate sequence.

This is intentionally small and explicit, so you can compare folder creation
and artifacts step-by-step on a clean lab data root.
#>

[CmdletBinding()]
param(
  [string]$Dataset = 'MiniInsurance',
  [switch]$SkipReset,
  [switch]$NoSnapshots
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot {
  param([string]$StartPath)
  $p = (Resolve-Path -LiteralPath $StartPath).Path
  while ($true) {
    if (Test-Path -LiteralPath (Join-Path $p 'src')) {
      $csproj = Join-Path $p 'src/EmbeddingShift.ConsoleEval/EmbeddingShift.ConsoleEval.csproj'
      if (Test-Path -LiteralPath $csproj) { return $p }
    }
    $parent = Split-Path -Parent $p
    if ($parent -eq $p) { throw "RepoRoot not found starting at '$StartPath'." }
    $p = $parent
  }
}

function Require-Env([string]$Name) {
  $v = [Environment]::GetEnvironmentVariable($Name, 'Process')
  if ([string]::IsNullOrWhiteSpace($v)) { throw "Missing environment variable: $Name" }
  return $v
}

function Snap([string]$Label, [string[]]$Paths, [string]$OutDir) {
  if ($NoSnapshots) { return $null }

  New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
  $out = Join-Path $OutDir ("{0}_{1}.txt" -f (Get-Date -Format "yyyyMMdd_HHmmss"), $Label)

  $lines = foreach ($p in $Paths) {
    if (Test-Path -LiteralPath $p) {
      Get-ChildItem -LiteralPath $p -Force -Recurse -ErrorAction SilentlyContinue |
        ForEach-Object { $_.FullName }
    } else {
      "[missing] $p"
    }
  }

  $lines | Set-Content -LiteralPath $out -Encoding utf8
  return $out
}

function Diff([string]$Prev, [string]$Now) {
  if ($NoSnapshots) { return @() }
  if ([string]::IsNullOrWhiteSpace($Prev) -or [string]::IsNullOrWhiteSpace($Now)) { return @() }

  Compare-Object (Get-Content -LiteralPath $Prev) (Get-Content -LiteralPath $Now) |
    Where-Object { $_.SideIndicator -eq "=>" } |
    Select-Object -ExpandProperty InputObject
}

$repoRoot = Resolve-RepoRoot -StartPath $PSScriptRoot
Set-Location -LiteralPath $repoRoot

$tenant = Require-Env 'EMBEDDINGSHIFT_TENANT'
$datasetRoot = Require-Env 'EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT'
$dataRoot = Require-Env 'EMBEDDINGSHIFT_DATA_ROOT'

$refsPath = Join-Path $datasetRoot 'policies'
$queriesPath = Join-Path $datasetRoot 'queries/queries.json'

if (-not (Test-Path -LiteralPath $refsPath)) { throw "Refs path not found: $refsPath" }
if (-not (Test-Path -LiteralPath $queriesPath)) { throw "Queries file not found: $queriesPath" }

$resultsRoot = Join-Path $repoRoot 'results'
$outDir = Join-Path $resultsRoot '_scratch/_snapshots'

$pathsToWatch = @(
  (Join-Path $resultsRoot '_scratch'),
  (Join-Path $resultsRoot ("insurance/tenants/{0}" -f $tenant)),
  (Join-Path $dataRoot ("embeddings/{0}" -f $Dataset)),
  (Join-Path $dataRoot ("manifests/{0}" -f $Dataset))
)

Write-Host "[Lab-Ingest] RepoRoot = $repoRoot"
Write-Host "[Lab-Ingest] Tenant  = $tenant"
Write-Host "[Lab-Ingest] Dataset = $Dataset"
Write-Host "[Lab-Ingest] DataRoot= $dataRoot"
Write-Host ""

$S0 = Snap -Label '00_before_status' -Paths $pathsToWatch -OutDir $outDir
dotnet run --project src/EmbeddingShift.ConsoleEval -- dataset-status $Dataset --role=all
$S1 = Snap -Label '01_after_status' -Paths $pathsToWatch -OutDir $outDir
Diff $S0 $S1 | ForEach-Object { Write-Host ("[new] {0}" -f $_) }

if (-not $SkipReset) {
  $S2a = Snap -Label '02_before_reset' -Paths $pathsToWatch -OutDir $outDir
  dotnet run --project src/EmbeddingShift.ConsoleEval -- dataset-reset $Dataset --role=all --force
  $S2b = Snap -Label '03_after_reset' -Paths $pathsToWatch -OutDir $outDir
  Diff $S2a $S2b | ForEach-Object { Write-Host ("[new] {0}" -f $_) }
}

$S3a = Snap -Label '04_before_ingest' -Paths $pathsToWatch -OutDir $outDir
dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-dataset "$refsPath" "$queriesPath" $Dataset
$S3b = Snap -Label '05_after_ingest' -Paths $pathsToWatch -OutDir $outDir
Diff $S3a $S3b | ForEach-Object { Write-Host ("[new] {0}" -f $_) }

dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-inspect $Dataset --role=refs
dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-inspect $Dataset --role=queries

dotnet run --project src/EmbeddingShift.ConsoleEval -- dataset-validate $Dataset --role=all

Write-Host ""
Write-Host "Next (optional):"
Write-Host "  dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance pipeline"
