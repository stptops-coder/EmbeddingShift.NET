Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $RepoRoot

$tenant = "insurer-a"
$ptr = Join-Path $RepoRoot "private\last-posneg-run.txt"
if (-not (Test-Path $ptr)) { throw "Missing pointer file: $ptr. Run 30-PosNegRun-Scale10.ps1 first." }

$runDir = (Get-Content $ptr -Raw).Trim()
Write-Host "RUN_DIR=$runDir"

$out = Join-Path $RepoRoot "private\segments.gap.tau0.ndcg3.json"

dotnet run --project ".\private\SegmenterTool\SegmenterTool\SegmenterTool.csproj" -- `
  --baseline "$runDir" --posneg "$runDir" `
  --rule gap-stability --tau 0.0 --metric ndcg@3 --eps 0 --out "$out"

dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant $tenant `
  domain mini-insurance segment-compare --segments "$out" --metric ndcg@3
