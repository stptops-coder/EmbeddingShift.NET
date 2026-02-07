param(
  [string]$Tenant = "insurer-a"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $RepoRoot

Write-Host "[PosNegRun] Creating posneg-run with scale=10 (semantic-hash, ngrams=1)..."
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant $Tenant `
  --sim-algo=semantic-hash --sim-char-ngrams=1 `
  domain mini-insurance posneg-run --scale=10

$runDir = (Get-ChildItem (Join-Path $RepoRoot "results\insurance\tenants\$Tenant") -Directory -Filter "mini-insurance-posneg-run_*" |
  Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName

Write-Host "RUN_DIR=$runDir"

# Persist pointer for next scripts
$ptr = Join-Path $RepoRoot "private\last-posneg-run.txt"
Set-Content -Path $ptr -Value $runDir -Encoding UTF8
Write-Host "[PosNegRun] Pointer written: $ptr"

# Ensure Top2 fields exist (required for gap-stability)
$posFile = Join-Path $runDir "eval.perQuery.posneg.json"
$hit = Select-String -Path $posFile -Pattern '"Top2Score"' -SimpleMatch | Select-Object -First 1
if (-not $hit) { throw "Top2Score not found in $posFile (gap-stability rule would be meaningless)." }

Write-Host "[PosNegRun] Top2Score present => gap-stability rule is supported."
