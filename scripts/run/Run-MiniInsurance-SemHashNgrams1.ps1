param(
  [int]$Seed = 1006,
  [string]$Tenant = "insurer-a",
  [string]$RepoRoot = "",
  [switch]$Open
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "..\lib\RepoRoot.ps1")
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
  $RepoRoot = Get-RepoRoot -StartPath $PSScriptRoot
}

# --- Preflight ---------------------------------------------------------------
if (-not (Test-Path $RepoRoot)) {
  throw "RepoRoot not found: $RepoRoot"
}

Set-Location $RepoRoot

if (-not (Test-Path "src\EmbeddingShift.ConsoleEval\EmbeddingShift.ConsoleEval.csproj")) {
  throw "This does not look like the repo root. Missing: src\EmbeddingShift.ConsoleEval\EmbeddingShift.ConsoleEval.csproj"
}

# Fixed "simulation profile" (keep it dead simple; edit here if needed)
$simArgs = @(
  "--tenant", $Tenant,
  "--backend=sim",
  "--sim-mode=deterministic",
  "--sim-algo=semantic-hash"
)

$dataset = "FirstLight3-$Seed"

# --- 1) Generate dataset -----------------------------------------------------
& dotnet run --project src/EmbeddingShift.ConsoleEval -- @simArgs `
  domain mini-insurance dataset-generate $dataset --seed=$Seed --stages=3 --policies=200 --queries=400 --overwrite

# --- 2) Point pipeline to stage-00 ------------------------------------------
$stage0 = Join-Path $RepoRoot "results\insurance\tenants\$Tenant\datasets\$dataset\stage-00"

if (-not (Test-Path $stage0)) {
  throw "Stage-00 not found after dataset-generate: $stage0"
}

$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $stage0

# --- 3) Pipeline (Baseline -> First -> Delta -> LearnedDelta -> Aggregates) ---
& dotnet run --project src/EmbeddingShift.ConsoleEval -- @simArgs `
  domain mini-insurance pipeline

# --- 4) Show best training result directory (posneg-best) --------------------
$bestOut = & dotnet run --project src/EmbeddingShift.ConsoleEval -- @simArgs `
  domain mini-insurance posneg-best

# Try to extract a directory path from output, robustly.
# We accept lines like:
#   "Best directory: <path>"
#   "BestDir = <path>"
#   "<path>" (fallback: first absolute path in output)
$bestDir = $null

$line = ($bestOut | Select-String -Pattern "Best\s*(directory|dir)\s*:\s*(.+)$" -CaseSensitive:$false | Select-Object -First 1)
if ($line) {
  $bestDir = $line.Matches[0].Groups[2].Value.Trim()
}
if (-not $bestDir) {
  $line2 = ($bestOut | Select-String -Pattern "BestDir\s*=\s*(.+)$" -CaseSensitive:$false | Select-Object -First 1)
  if ($line2) {
    $bestDir = $line2.Matches[0].Groups[1].Value.Trim()
  }
}
if (-not $bestDir) {
  $line3 = ($bestOut | Select-String -Pattern "([A-Za-z]:\\[^`"<>|]+)" | Select-Object -First 1)
  if ($line3) {
    $bestDir = $line3.Matches[0].Groups[1].Value.Trim()
  }
}

""
"BestDir = $bestDir"
"Stage0  = $stage0"
""

# --- 5) Show effect (MAP/NDCG) ----------------------------------------------
& dotnet run --project src/EmbeddingShift.ConsoleEval -- @simArgs `
  domain mini-insurance posneg-run

if ($Open) {
  if ($bestDir -and (Test-Path $bestDir)) { ii $bestDir }
  else { ii (Join-Path $RepoRoot "results\insurance\tenants\$Tenant") }
}
