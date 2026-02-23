#requires -Version 5.1

# =========================
# PosNeg BIG Runbook (deterministic sim) – FULL PACKAGE
# =========================
# Prep -> Build -> N big PosNeg runs (multi-stage dataset, stage-by-stage) -> Tests -> Scratch layout inspection.
#
# Key points:
# - Every stage run gets its OWN scratch RunRoot to avoid mixing runs from different stages.
# - PosNeg runs live under: runs\_repo\MiniInsurance-PosNeg (hidden by default); we enable it via --include-repo-posneg.
# - Repo PosNeg experiments always use a dedicated --profile key (suffix '__repo-posneg') to keep actives isolated.
#
# Usage (PowerShell):
#   cd C:\pg\RakeX
#   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
#   .\scripts\runbook-experimental\35-PosNegBigRunAll-Deterministic.ps1
#
[CmdletBinding()]
param(
  # Root placement:
  #   - scratch (default): results\_scratch under the repo
  #   - temp: %TEMP%\EmbeddingShift.PosNegBig
  [ValidateSet('scratch','temp')]
  [string]$RootMode = 'scratch',

  [string]$Tenant = 'insurer-a',
  [int]$Seed = 1337,

  # Dataset generator (stage-00 baseline, stage-01 corpus drift, stage-02 query drift)
  [string]$DsName = 'PosNegBigDS',
  [int]$Stages = 3,
  [int]$Policies = 500,
  [int]$Queries = 1000,
  # Default = drift-only stages (faster). Use -StageIndices 0,1,2 to include baseline stage-00.
  [int[]]$StageIndices = @(1,2),

  # PosNeg settings
  [ValidateSet('micro','production','prod')]
  [string]$TrainMode = 'production',
  [int]$HardNegTopK = 5,
  # Default = single scale (faster). Override e.g. -PosNegScales 1,2,5 for sweeps.
  [double[]]$PosNegScales = @(1.0),

  # Pipeline toggle: by default we skip LearnedDelta inside the pipeline to avoid duplicate work
  # (we run explicit posneg-train + posneg-run anyway).
  [switch]$IncludeLearnedDelta,

  # Runs compare / decide / promote
  [string]$Metric = 'ndcg@3',
  [int]$Top = 10,
  [bool]$IncludeRepoPosNeg = $true,
  [bool]$CompareRepoPosNeg = $true,
  [switch]$Promote,

# Scenario folder under results\_scratch (scratch mode). Allows isolating matrix runs from single runs.
[string]$Scenario = 'EmbeddingShift.PosNegBig',

# Performance toggles for wrappers (matrix scripts):
[switch]$SkipPrep,
[switch]$SkipBuild,
[switch]$SkipTests,
[switch]$SkipInspect,

  # Simulation settings (deterministic)
  [string]$SimAlgo = 'semantic-hash',
  [int]$SimSemanticCharNGrams = 1
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot {
  param([string]$ScriptRoot)

  $candidates = @()
  $candidates += (Get-Location).Path
  $candidates += (Resolve-Path (Join-Path $ScriptRoot '..\..')).Path
  $candidates += (Resolve-Path (Join-Path $ScriptRoot '..\..\..')).Path
  $candidates = $candidates | Select-Object -Unique

  foreach ($c in $candidates) {
    if (Test-Path (Join-Path $c '.git') -PathType Container) { return $c }
    if (Test-Path (Join-Path $c 'EmbeddingShift.sln') -PathType Leaf) { return $c }
  }

  throw "Cannot resolve RepoRoot. Checked: $($candidates -join '; ')"
}

function New-RunRoot {
  param(
    [Parameter(Mandatory=$true)][string]$RepoRoot,
    [Parameter(Mandatory=$true)][string]$Mode,
    [Parameter(Mandatory=$true)][string]$Scenario,
    [Parameter(Mandatory=$true)][string]$Suffix
  )

  $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
  if ($Mode -eq 'temp') {
    $base = Join-Path $env:TEMP 'EmbeddingShift.PosNegBig'
    return (Join-Path $base ("{0}__{1}" -f $stamp, $Suffix))
  }

  return (Join-Path $RepoRoot ("results\_scratch\{0}\{1}__{2}" -f $Scenario, $stamp, $Suffix))
}

function Invoke-ConsoleEval {
  param(
    [Parameter(Mandatory=$true)][string]$Project,
    [Parameter(Mandatory=$true)][string[]]$Args
  )

  & dotnet run --project $Project -- @Args
  if ($LASTEXITCODE -ne 0) { throw "ConsoleEval failed with exit code $LASTEXITCODE" }
}

function Normalize-ProfileKey {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory=$false)][string]$ProfileKey,
    [Parameter(Mandatory=$false)][int]$MaxLen = 120
  )

  # PowerShell 5.1 compatible null handling (no '??')
  $s = ''
  if ($null -ne $ProfileKey) { $s = "$ProfileKey" }
  $s = $s.Trim()

  if ([string]::IsNullOrWhiteSpace($s)) { $s = 'default' }

  # Keep same character class approach as the C# side (safe folder name)
  $s = ($s -replace '[^a-zA-Z0-9_\-\.]+', '_')

  if ($s.Length -gt $MaxLen) { $s = $s.Substring(0, $MaxLen) }

  return $s
}

$repoRoot = Resolve-RepoRoot -ScriptRoot $PSScriptRoot
Set-Location $repoRoot

$proj = 'src\EmbeddingShift.ConsoleEval'
$domain = 'insurance'
$backend = 'sim'
$simMode = 'deterministic'
$scenario = $Scenario
Write-Host "[PosNegBig] RepoRoot = $repoRoot"
Write-Host "[PosNegBig] Tenant  = $Tenant"
Write-Host "[PosNegBig] Seed    = $Seed"

# 0) Prep + Build
if (-not $SkipPrep) { & (Join-Path $repoRoot 'scripts\runbook\00-Prep.ps1') }
if (-not $SkipBuild) { & (Join-Path $repoRoot 'scripts\runbook\10-Build.ps1') }

$roots = New-Object System.Collections.Generic.List[string]

# Compute dataset name once (shared across all stage runs)
$datasetName = ("{0}_p{1}_q{2}_seed{3}_st{4}" -f $DsName, $Policies, $Queries, $Seed, $Stages)
$datasetName = ($datasetName -replace '[^a-zA-Z0-9_\-\.]+' , '_')

# 1) Generate dataset ONCE in its own host root (avoids re-generating stage-00/01/02 for every stage run)
$datasetSuffix = ("tenant={0}__p{1}__q{2}__stages{3}__dataset" -f $Tenant, $Policies, $Queries, $Stages)
$datasetSuffix = ($datasetSuffix -replace '[^a-zA-Z0-9_\-\.=]+' , '_')
$datasetHostRoot = New-RunRoot -RepoRoot $repoRoot -Mode $RootMode -Scenario $scenario -Suffix $datasetSuffix

if (Test-Path $datasetHostRoot) { Remove-Item $datasetHostRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $datasetHostRoot | Out-Null

$env:EMBEDDINGSHIFT_ROOT           = $datasetHostRoot
$env:EMBEDDINGSHIFT_RESULTS_DOMAIN = $domain
$env:EMBEDDINGSHIFT_LAYOUT         = 'tenant'
$env:EMBEDDINGSHIFT_TENANT         = $Tenant

$env:EMBEDDINGSHIFT_BACKEND        = $backend
$env:EMBEDDINGSHIFT_SIM_MODE       = $simMode
$env:EMBEDDINGSHIFT_SIM_ALGO       = $SimAlgo
$env:EMBEDDINGSHIFT_SIM_SEMANTIC_CHAR_NGRAMS = "$SimSemanticCharNGrams"
$env:EMBEDDING_SIM_ALGO            = $SimAlgo
$env:EMBEDDING_SIM_SEMANTIC_CHAR_NGRAMS = "$SimSemanticCharNGrams"

Write-Host ""
Write-Host ("[PosNegBig] DATASET HOST ROOT = {0}" -f $datasetHostRoot)
Invoke-ConsoleEval -Project $proj -Args @(
  '--tenant', $Tenant,
  "--backend=$backend", "--sim-mode=$simMode", "--sim-algo=$SimAlgo", "--sim-char-ngrams=$SimSemanticCharNGrams",
  'domain','mini-insurance','dataset-generate', $datasetName,
  '--stages', "$Stages", '--policies', "$Policies", '--queries', "$Queries", '--seed', "$Seed", '--overwrite'
)

foreach ($stage in $StageIndices) {
  if ($stage -lt 0 -or $stage -ge $Stages) {
    throw "StageIndices contains $stage, but Stages=$Stages. Valid stages are 0..$($Stages-1)."
  }

  $suffix = ("tenant={0}__p{1}__q{2}__stages{3}__stage{4:00}" -f $Tenant, $Policies, $Queries, $Stages, $stage)
  $suffix = ($suffix -replace '[^a-zA-Z0-9_\-\.=]+', '_')
  $root = New-RunRoot -RepoRoot $repoRoot -Mode $RootMode -Scenario $scenario -Suffix $suffix

  if (Test-Path $root) { Remove-Item $root -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $root | Out-Null
  $roots.Add($root) | Out-Null

  # Keep process environment coherent for follow-up commands.
  $env:EMBEDDINGSHIFT_ROOT           = $root
  $env:EMBEDDINGSHIFT_RESULTS_DOMAIN = $domain
  $env:EMBEDDINGSHIFT_LAYOUT         = 'tenant'
  $env:EMBEDDINGSHIFT_TENANT         = $Tenant

  $env:EMBEDDINGSHIFT_BACKEND        = $backend
  $env:EMBEDDINGSHIFT_SIM_MODE       = $simMode
  $env:EMBEDDINGSHIFT_SIM_ALGO       = $SimAlgo
  $env:EMBEDDINGSHIFT_SIM_SEMANTIC_CHAR_NGRAMS = "$SimSemanticCharNGrams"
  $env:EMBEDDING_SIM_ALGO            = $SimAlgo
  $env:EMBEDDING_SIM_SEMANTIC_CHAR_NGRAMS = "$SimSemanticCharNGrams"

  Write-Host ""
  Write-Host "============================================================"
  Write-Host ("[PosNegBig] ROOT     = {0}" -f $env:EMBEDDINGSHIFT_ROOT)
  Write-Host ("[PosNegBig] MODE     = {0}/{1} (algo={2}, ngrams={3})" -f $backend, $simMode, $SimAlgo, $SimSemanticCharNGrams)
  Write-Host ("[PosNegBig] DATASET  = {0} (stages={1}, policies={2}, queries={3})" -f $DsName, $Stages, $Policies, $Queries)
  Write-Host ("[PosNegBig] STAGE    = stage-{0:00}" -f $stage)
  Write-Host "============================================================"

  $datasetName = ("{0}_p{1}_q{2}_seed{3}_st{4}" -f $DsName, $Policies, $Queries, $Seed, $Stages)
  $datasetName = ($datasetName -replace '[^a-zA-Z0-9_\-\.]+', '_')

  # 2) Point dataset root to the chosen stage (from shared dataset host root)
  $stageFolder = ("stage-{0:00}" -f $stage)
  $datasetRoot = Join-Path $datasetHostRoot ("results\{0}\tenants\{1}\datasets\{2}\{3}" -f $domain, $Tenant, $datasetName, $stageFolder)
  $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $datasetRoot
  $env:EMBEDDINGSHIFT_DATASET_ROOT = $datasetRoot
  Write-Host ("[PosNegBig] DATASET_ROOT = {0}" -f $datasetRoot)

  # 3) Pipeline (Baseline -> FirstShift -> First+Delta)
  # By default we skip LearnedDelta inside the pipeline (duplicate work; we run explicit posneg-train + posneg-run).
  $pipelineArgs = @(
    '--tenant', $Tenant,
    "--backend=$backend", "--sim-mode=$simMode", "--sim-algo=$SimAlgo", "--sim-char-ngrams=$SimSemanticCharNGrams",
    'domain','mini-insurance','pipeline'
  )
  if (-not $IncludeLearnedDelta) { $pipelineArgs += '--no-learned' }
  Invoke-ConsoleEval -Project $proj -Args $pipelineArgs

  # 4) PosNeg TRAIN (stage-specific because DATASET_ROOT points to the stage folder)
  $modeArg = if ($TrainMode -eq 'prod') { 'production' } else { $TrainMode }
  Invoke-ConsoleEval -Project $proj -Args @(
    '--tenant', $Tenant,
    "--backend=$backend", "--sim-mode=$simMode", "--sim-algo=$SimAlgo", "--sim-char-ngrams=$SimSemanticCharNGrams",
    'domain','mini-insurance','posneg-train', "--mode=$modeArg", "--hardneg-topk=$HardNegTopK"
  )

  # 5) PosNeg RUN (scale sweep) – store as internal repo runs under runs\_repo\MiniInsurance-PosNeg
  foreach ($scale in $PosNegScales) {
    Invoke-ConsoleEval -Project $proj -Args @(
      '--tenant', $Tenant,
      "--backend=$backend", "--sim-mode=$simMode", "--sim-algo=$SimAlgo", "--sim-char-ngrams=$SimSemanticCharNGrams",
      'domain','mini-insurance','posneg-run', '--latest', ("--scale={0}" -f $scale)
    )
  }

  # 6) Compare (normal runs)
  $runsRoot = Join-Path $env:EMBEDDINGSHIFT_ROOT ("results\{0}\tenants\{1}\runs" -f $domain, $Tenant)
  $compareDir = Join-Path $runsRoot '_compare'
  Invoke-ConsoleEval -Project $proj -Args @(
    '--tenant', $Tenant,
    'runs-compare', '--runs-root', $runsRoot, '--metric', $Metric, '--top', "$Top", '--write', '--out', $compareDir
  )

  # Optional: compare internal repo PosNeg runs (explicit root)
  if ($CompareRepoPosNeg) {
    $posNegRoot = Join-Path $runsRoot '_repo\MiniInsurance-PosNeg'
    if (Test-Path $posNegRoot) {
      $compareRepoDir = Join-Path $compareDir 'repo-posneg'
      Invoke-ConsoleEval -Project $proj -Args @(
        '--tenant', $Tenant,
        'runs-compare', '--runs-root', $posNegRoot, '--metric', $Metric, '--top', "$Top", '--write', '--out', $compareRepoDir
      )
    }
    else {
      Write-Host ("[PosNegBig] repo-posneg root not found: {0} (skipping repo compare)" -f $posNegRoot)
    }
  }

  # 7) Decide (+ optional promote). IMPORTANT: isolate repo experiments under their own profile.
  $profileKeyRaw = ("{0}_{1}__{2}__ng{3}__ds{4}__seed{5}__stages{6}__p{7}__q{8}__stage{9:00}__repo-posneg" -f $backend, $simMode, $SimAlgo, $SimSemanticCharNGrams, $datasetName, $Seed, $Stages, $Policies, $Queries, $stage)
  $profileKey = Normalize-ProfileKey -ProfileKey $profileKeyRaw -MaxLen 120

  $activeDir = Join-Path $runsRoot ("_active\profiles\{0}" -f $profileKey)
  $activeFileName = ("active_{0}.json" -f $Metric)
  $activeFile = Join-Path $activeDir $activeFileName

  $sharedActiveDir = Join-Path $repoRoot ("results\_scratch\_active\{0}\tenants\{1}\runs\_active\profiles\{2}" -f $domain, $Tenant, $profileKey)
  $sharedActiveFile = Join-Path $sharedActiveDir $activeFileName

  # Restore shared active pointer into this run root BEFORE runs-decide.
  New-Item -ItemType Directory -Force -Path $activeDir | Out-Null
  if (Test-Path -LiteralPath $sharedActiveFile -PathType Leaf) {
    Copy-Item -LiteralPath $sharedActiveFile -Destination $activeFile -Force
    Write-Host ("[PosNegBig] Shared active restored: {0}" -f $sharedActiveFile)
  }

  $decideArgs = @(
    '--tenant', $Tenant,
    'runs-decide', '--runs-root', $runsRoot, '--metric', $Metric, '--profile', $profileKey, '--write'
  )
  if ($IncludeRepoPosNeg) { $decideArgs += '--include-repo-posneg' }
  if ($Promote) { $decideArgs += '--apply' }
  Invoke-ConsoleEval -Project $proj -Args $decideArgs

  # Persist active pointer into the shared active area (so separate scratch roots can re-use it).
  if (Test-Path -LiteralPath $activeFile -PathType Leaf) {
    New-Item -ItemType Directory -Force -Path $sharedActiveDir | Out-Null
    Copy-Item -LiteralPath $activeFile -Destination $sharedActiveFile -Force
    Write-Host ("[PosNegBig] Shared active updated: {0}" -f $sharedActiveFile)
  }
}

Write-Host ""
Write-Host "[PosNegBig] Stage runs completed:"
$roots | ForEach-Object { Write-Host ("  - {0}" -f $_) }

# 8) Tests (isolated tests-data + _tmp happens inside 30-Tests.ps1)
if (-not $SkipTests) { & (Join-Path $repoRoot 'scripts\runbook\30-Tests.ps1') }

# 9) Layout inspection (show last run root) – only meaningful for scratch mode
$lastRoot = $roots[$roots.Count - 1]
if (($RootMode -eq 'scratch') -and (-not $SkipInspect)) {
  & (Join-Path $repoRoot 'scripts\runbook\60-Inspect-ScratchLayout.ps1') -Root $lastRoot
}

Write-Host ""
Write-Host "[PosNegBig] DONE. LastRoot: $lastRoot"
