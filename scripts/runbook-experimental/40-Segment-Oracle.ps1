param(
  [string]$Tenant = "insurer-a",
  [string]$Metric = "ndcg@3"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $RepoRoot

$ptr = Join-Path $RepoRoot "private\last-posneg-run.txt"
if (-not (Test-Path $ptr)) { throw "Missing pointer file: $ptr. Run 30-PosNegRun-Scale10.ps1 first." }

$runDir = (Get-Content $ptr -Raw).Trim()
Write-Host "RUN_DIR=$runDir"

$out = Join-Path $RepoRoot "private\segments.oracle.$($Metric.Replace('@','')).eps0.json"

dotnet run --project ".\private\SegmenterTool\SegmenterTool\SegmenterTool.csproj" -- `
  --baseline "$runDir" --posneg "$runDir" `
  --rule oracle --metric $Metric --eps 0 --out "$out"

dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant $Tenant `
  domain mini-insurance segment-compare --segments "$out" --metric $Metric
