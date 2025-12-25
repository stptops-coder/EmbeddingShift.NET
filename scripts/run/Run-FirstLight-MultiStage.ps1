[CmdletBinding()]
param(
  # Optional override. If not provided, the script auto-detects the repo root.
  [string]$RepoRoot,

  # Folder name under .\results\<ResultsDomain>\...
  [string]$ResultsDomain = "insurance",

  # ConsoleEval domain id (argument to the CLI).
  [string]$DomainId = "mini-insurance",

  # Naming
  [string]$BaseDatasetName = "FirstLight3",

  # Generator size
  [int]$Policies = 80,
  [int]$Queries  = 160,
  [int]$Stages   = 3,

  # Seeds => 5 runs
  [int[]]$Seeds = @(1337, 1338, 1339, 1340, 1341),

  # Which stage is used for training / which stages are evaluated
  [int]$TrainStageIndex = 1,
  [int[]]$RunStageIndices = @(1, 0, 2),

  # PosNeg training options
  [ValidateSet("micro","production")]
  
  # [string]$TrainMode = "micro",
  # [double]$CancelEpsilon = 0.000001,
  
  [string]$TrainMode = "production",
  [double]$CancelEpsilon = 0.001,



  # Simulation
  [ValidateSet("deterministic","stochastic")]
  [string]$SimMode = "deterministic",
  [double]$SimNoise = 0,
  # [string]$SimAlgo = "semantic-hash",
  [string]$SimAlgo = "sha256",
  [int]$SimCharNgrams = 1,

  # Semantic cache
  [switch]$SemanticCache,
  [int]$CacheMax = 20000,
  [int]$CacheHamming = 3,
  [int]$CacheApprox = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$InvCult = [System.Globalization.CultureInfo]::InvariantCulture

# --- Import helpers
. (Join-Path $PSScriptRoot "..\lib\RepoRoot.ps1")
. (Join-Path $PSScriptRoot "..\lib\DotNet.ps1")

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
  # Convention: scripts\run\... lives under <RepoRoot>\scripts\...
  # So the repo root is two levels up from this script folder.
  $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
}
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

Assert-File (Join-Path $RepoRoot "EmbeddingShift.sln") "Solution"
Assert-Dir  (Join-Path $RepoRoot "src\EmbeddingShift.ConsoleEval") "ConsoleEval project"

function Parse-Num([string]$s) {
  if ([string]::IsNullOrWhiteSpace($s)) { return $null }

  $t = $s.Trim()

  # Remove trailing punctuation (e.g. "0.259.")
  while ($t.Length -gt 0 -and ($t.EndsWith(".") -or $t.EndsWith(",") -or $t.EndsWith(";"))) {
    $t = $t.Substring(0, $t.Length - 1)
  }

  $t = $t -replace '\s+', ''

  # German style: 1.234,56  -> 1234.56
  if ($t -match '^\d{1,3}(\.\d{3})+,\d+$') {
    $t = $t -replace '\.', ''
    $t = $t -replace ',', '.'
  } else {
    # Simple: 0,259 -> 0.259
    $t = $t -replace ',', '.'
  }

  return [double]::Parse($t, $InvCult)
}

function Parse-RunMetrics([string]$text) {
  $m = [ordered]@{
    BaselineMap  = $null
    PosNegMap    = $null
    DeltaMap     = $null
    BaselineNdcg = $null
    PosNegNdcg   = $null
    DeltaNdcg    = $null
  }

  $lines = $text -split "`r?`n"

  # We keep this tolerant: match first occurrence of each metric line.
  foreach ($line in $lines) {
if ($m.BaselineMap -eq $null -and $line -match 'baseline.*map@1\s*[:=]\s*([0-9\.,Ee\+\-]+)') { $m.BaselineMap = Parse-Num $Matches[1] }
if ($m.PosNegMap   -eq $null -and $line -match 'posneg.*map@1\s*[:=]\s*([0-9\.,Ee\+\-]+)')   { $m.PosNegMap   = Parse-Num $Matches[1] }
if ($m.DeltaMap    -eq $null -and $line -match 'delta.*map@1\s*[:=]\s*([0-9\.,Ee\+\-]+)')    { $m.DeltaMap    = Parse-Num $Matches[1] }

if ($m.BaselineNdcg -eq $null -and $line -match 'baseline.*ndcg@3\s*[:=]\s*([0-9\.,Ee\+\-]+)') { $m.BaselineNdcg = Parse-Num $Matches[1] }
if ($m.PosNegNdcg   -eq $null -and $line -match 'posneg.*ndcg@3\s*[:=]\s*([0-9\.,Ee\+\-]+)')   { $m.PosNegNdcg   = Parse-Num $Matches[1] }
if ($m.DeltaNdcg    -eq $null -and $line -match 'delta.*ndcg@3\s*[:=]\s*([0-9\.,Ee\+\-]+)')    { $m.DeltaNdcg    = Parse-Num $Matches[1] }
  }

  return [pscustomobject]$m
}

# ---- RunRoot: everything for this execution goes under one directory
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$runRoot = Join-Path $RepoRoot ("results\{0}\runroots\{1}_{2}" -f $ResultsDomain, $BaseDatasetName, $ts)
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

# ---- env var backup/restore (Process scope)
$envNames = @(
  "EMBEDDINGSHIFT_ROOT",
  "EMBEDDING_SIM_MODE",
  "EMBEDDING_SIM_NOISE",
  "EMBEDDING_SIM_ALGO",
  "EMBEDDING_SIM_CHAR_NGRAMS",
  "EMBEDDING_SEMANTIC_CACHE",
  "EMBEDDING_SEMANTIC_CACHE_MAX",
  "EMBEDDING_SEMANTIC_CACHE_HAMMING",
  "EMBEDDING_SEMANTIC_CACHE_APPROX",
  "EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT"
)

$prev = @{}
foreach ($n in $envNames) {
  $prev[$n] = [Environment]::GetEnvironmentVariable($n, "Process")
}

function Restore-Env() {
  foreach ($n in $envNames) {
    [Environment]::SetEnvironmentVariable($n, $prev[$n], "Process")
  }
  Write-Host "[Env] Restored previous env (Process scope)."
}

try {
  # Route all artifacts under runRoot
  [Environment]::SetEnvironmentVariable("EMBEDDINGSHIFT_ROOT", $runRoot, "Process")
  [Environment]::SetEnvironmentVariable("EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT", $null, "Process")

  # Sim settings
  [Environment]::SetEnvironmentVariable("EMBEDDING_SIM_MODE", $SimMode, "Process")
  [Environment]::SetEnvironmentVariable("EMBEDDING_SIM_NOISE", $SimNoise.ToString($InvCult), "Process")
  [Environment]::SetEnvironmentVariable("EMBEDDING_SIM_ALGO", $SimAlgo, "Process")
  [Environment]::SetEnvironmentVariable("EMBEDDING_SIM_CHAR_NGRAMS", "$SimCharNgrams", "Process")

  # Semantic cache settings
  if ($SemanticCache) {
    [Environment]::SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE", "1", "Process")
    [Environment]::SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE_MAX", "$CacheMax", "Process")
    [Environment]::SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE_HAMMING", "$CacheHamming", "Process")
    [Environment]::SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE_APPROX", "$CacheApprox", "Process")
  } else {
    [Environment]::SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE", $null, "Process")
    [Environment]::SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE_MAX", $null, "Process")
    [Environment]::SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE_HAMMING", $null, "Process")
    [Environment]::SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE_APPROX", $null, "Process")
  }

  Write-Host ""
  Write-Host "dotnet build (explicit sln)"
  Invoke-DotNet -Args @("build", (Join-Path $RepoRoot "EmbeddingShift.sln")) | Out-Host

  $manifest = [ordered]@{
    createdUtc = (Get-Date).ToUniversalTime().ToString("o")
    repoRoot = $RepoRoot
    runRoot = $runRoot
    resultsDomain = $ResultsDomain
    domainId = $DomainId
    generator = [ordered]@{ policies = $Policies; queries = $Queries; stages = $Stages; seeds = $Seeds }
    simulation = [ordered]@{ mode = $SimMode; noise = $SimNoise; algo = $SimAlgo; charNgrams = $SimCharNgrams }
    semanticCache = [ordered]@{ enabled = [bool]$SemanticCache; max = $CacheMax; hamming = $CacheHamming; approx = $CacheApprox }
    trainStageIndex = $TrainStageIndex
    runStageIndices = $RunStageIndices
  }

  $cases = @()

  foreach ($seed in $Seeds) {
    $datasetName = "$BaseDatasetName-$seed"

    Write-Host ""
    Write-Host ("=== Seed {0} / dataset {1} ===" -f $seed, $datasetName)

    # 1) dataset-generate
    $genArgs = @(
      "run","--project","src/EmbeddingShift.ConsoleEval","--",
      "domain", $DomainId, "dataset-generate",
      $datasetName,
      ("--policies=" + $Policies),
      ("--queries=" + $Queries),
      ("--stages=" + $Stages),
      ("--seed=" + $seed)
    )
    Invoke-DotNet -Args $genArgs -WorkingDirectory $RepoRoot | Out-Host

    # 2) train (posneg)
    $trainStagePath = Join-Path $runRoot ("results\{0}\datasets\{1}\stage-{2:00}" -f $ResultsDomain, $datasetName, $TrainStageIndex)
    [Environment]::SetEnvironmentVariable("EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT", $trainStagePath, "Process")

    $trainArgs = @(
      "run","--project","src/EmbeddingShift.ConsoleEval","--",
      "domain", $DomainId, "posneg-train",
      ("--mode=" + $TrainMode),
      ("--cancel-epsilon=" + $CancelEpsilon.ToString($InvCult))
    )
    Invoke-DotNet -Args $trainArgs -WorkingDirectory $RepoRoot | Out-Host

# 3) run stages
    foreach ($runStage in $RunStageIndices) {
      $runStagePath = Join-Path $runRoot ("results\{0}\datasets\{1}\stage-{2:00}" -f $ResultsDomain, $datasetName, $runStage)
      [Environment]::SetEnvironmentVariable("EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT", $runStagePath, "Process")

      $runArgs = @(
        "run","--project","src/EmbeddingShift.ConsoleEval","--",
        "domain", $DomainId, "posneg-run"
      )


      $out = Invoke-DotNet -Args $runArgs -WorkingDirectory $RepoRoot
      $out | Out-Host

      $metrics = Parse-RunMetrics $out
      $cases += [pscustomobject]@{
        dataset = $datasetName
        seed = $seed
        trainStage = $TrainStageIndex
        runStage = $runStage
        baseline_map1  = $metrics.BaselineMap
        posneg_map1    = $metrics.PosNegMap
        delta_map1     = $metrics.DeltaMap
        baseline_ndcg3 = $metrics.BaselineNdcg
        posneg_ndcg3   = $metrics.PosNegNdcg
        delta_ndcg3    = $metrics.DeltaNdcg
      }
    }
  }

  # Write JSON outputs (no CSV)
  $manifestPath = Join-Path $runRoot "manifest.json"
  $casesPath    = Join-Path $runRoot "cases.json"
  ($manifest | ConvertTo-Json -Depth 10) | Set-Content -Encoding UTF8 -Path $manifestPath
  ($cases    | ConvertTo-Json -Depth 10) | Set-Content -Encoding UTF8 -Path $casesPath

  Write-Host ""
  Write-Host "=== DONE ==="
  Write-Host ("RunRoot   : {0}" -f $runRoot)
  Write-Host ("Manifest  : {0}" -f $manifestPath)
  Write-Host ("Cases     : {0}" -f $casesPath)

  # Print runRoot for callers that want to capture it
  Write-Output $runRoot
}
finally {
  Restore-Env
}
