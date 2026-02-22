#requires -Version 5.1

# =========================
# PosNeg BIG – Summary Collector
# =========================
# Scans a scratch scenario folder (results\_scratch\<Scenario>) for runs-decide decision JSON files,
# extracts key parameters (seed/policies/queries/stage), and writes:
#   - summary_*.csv
#   - summary_*.md
#
# Usage:
#   .\scripts\runbook-experimental\37-PosNegBigSummarize.ps1 -Scenario EmbeddingShift.PosNegBigMatrix -Tenant insurer-a -Metric ndcg@3
#
[CmdletBinding()]
param(
  [string]$Scenario = 'EmbeddingShift.PosNegBigMatrix',
  [string]$Tenant = 'insurer-a',
  [string]$Domain = 'insurance',
  [string]$Metric = 'ndcg@3',
  [string]$OutDir = ''
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

function Get-Prop {
  param(
    [Parameter(Mandatory=$true)]$Obj,
    [Parameter(Mandatory=$true)][string]$Name
  )
  if ($null -eq $Obj) { return $null }
  if ($Obj.PSObject.Properties.Match($Name).Count -gt 0) { return $Obj.$Name }
  return $null
}

function TryParse-FromProfile {
  param([string]$Profile)

  $seed = $null
  $p = $null
  $q = $null
  $stage = $null

  if ($Profile -match '__seed(?<seed>\d+)__') { $seed = [int]$Matches['seed'] }
  if ($Profile -match '__p(?<p>\d+)__q(?<q>\d+)__stage(?<s>\d{2})__') {
    $p = [int]$Matches['p']
    $q = [int]$Matches['q']
    $stage = [int]$Matches['s']
  }

  return @{
    Seed = $seed
    Policies = $p
    Queries = $q
    Stage = $stage
  }
}

$repoRoot = Resolve-RepoRoot -ScriptRoot $PSScriptRoot
Set-Location $repoRoot

$scenarioRoot = Join-Path $repoRoot ("results\_scratch\{0}" -f $Scenario)
if (-not (Test-Path -LiteralPath $scenarioRoot -PathType Container)) {
  throw "Scenario folder not found: $scenarioRoot"
}

if ([string]::IsNullOrWhiteSpace($OutDir)) {
  $OutDir = Join-Path $scenarioRoot '_matrix'
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Write-Host "[PosNegSummary] ScenarioRoot = $scenarioRoot"
Write-Host "[PosNegSummary] OutDir       = $OutDir"
Write-Host "[PosNegSummary] Metric       = $Metric"
Write-Host "[PosNegSummary] Tenant       = $Tenant"

# Decision files (we filter by regex because -Filter can behave oddly with '@' in patterns)
$allDecisionFiles = Get-ChildItem -Path $scenarioRoot -Recurse -File -ErrorAction Stop | Where-Object { $_.Name -match '^decision_.*\.json$' }
$decisionFiles = $allDecisionFiles | Where-Object { $_.Name -match ('^decision_{0}_.*\.json$' -f [regex]::Escape($Metric)) }

if (-not $decisionFiles -or $decisionFiles.Count -eq 0) {
  Write-Host "[PosNegSummary] No decision files found for metric '$Metric'."
  exit 0
}

$rows = New-Object System.Collections.Generic.List[object]

foreach ($f in $decisionFiles) {
  $raw = Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8
  $j = $raw | ConvertFrom-Json

  $candidate = Get-Prop -Obj $j -Name 'candidate'
  if ($null -eq $candidate) { $candidate = Get-Prop -Obj $j -Name 'Candidate' }

  $active = Get-Prop -Obj $j -Name 'active'
  if ($null -eq $active) { $active = Get-Prop -Obj $j -Name 'Active' }

  $profile = Get-Prop -Obj $j -Name 'profile'
  if ($null -eq $profile) { $profile = Get-Prop -Obj $j -Name 'Profile' }

  $decision = Get-Prop -Obj $j -Name 'decision'
  if ($null -eq $decision) { $decision = Get-Prop -Obj $j -Name 'Decision' }

  $reason = Get-Prop -Obj $j -Name 'reason'
  if ($null -eq $reason) { $reason = Get-Prop -Obj $j -Name 'Reason' }

  $candRunId = Get-Prop -Obj $candidate -Name 'runId'
  $candScore = Get-Prop -Obj $candidate -Name 'score'

  $activeRunId = Get-Prop -Obj $active -Name 'runId'
  $activeScore = Get-Prop -Obj $active -Name 'score'

  $parsed = TryParse-FromProfile -Profile ([string]$profile)

  # Derive runroot (the scratch run folder) from the path
  $full = $f.FullName
  $marker = ("\results\{0}\tenants\{1}\" -f $Domain, $Tenant)
  $idx = $full.IndexOf($marker, [System.StringComparison]::OrdinalIgnoreCase)
  $runRoot = if ($idx -gt 0) { $full.Substring(0, $idx) } else { '' }

  $rows.Add([pscustomobject]@{
    Seed        = $parsed.Seed
    Policies    = $parsed.Policies
    Queries     = $parsed.Queries
    Stage       = $parsed.Stage
    Metric      = $Metric
    Decision    = [string]$decision
    CandidateRunId = [string]$candRunId
    CandidateScore = $candScore
    ActiveRunId = [string]$activeRunId
    ActiveScore = $activeScore
    Profile     = [string]$profile
    Reason      = [string]$reason
    RunRoot     = $runRoot
    DecisionFile = $full
  }) | Out-Null
}

# Sort for readability
$sorted = $rows | Sort-Object Seed, Policies, Queries, Stage, CandidateScore -Descending

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$csvPath = Join-Path $OutDir ("summary_{0}_{1}.csv" -f ($Metric -replace '[^a-zA-Z0-9\-]+','_'), $stamp)
$mdPath  = Join-Path $OutDir ("summary_{0}_{1}.md"  -f ($Metric -replace '[^a-zA-Z0-9\-]+','_'), $stamp)

$sorted | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8

# Markdown table
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# PosNeg Matrix Summary") | Out-Null
$lines.Add("") | Out-Null
$lines.Add(("Scenario: `{0}`" -f $Scenario)) | Out-Null
$lines.Add(("Tenant: `{0}`" -f $Tenant)) | Out-Null
$lines.Add(("Metric: `{0}`" -f $Metric)) | Out-Null
$lines.Add(("Generated: {0}" -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))) | Out-Null
$lines.Add("") | Out-Null
$lines.Add("| Seed | Policies | Queries | Stage | Decision | CandidateScore | CandidateRunId | ActiveRunId |") | Out-Null
$lines.Add("|---:|---:|---:|---:|---|---:|---|---|") | Out-Null

foreach ($r in $sorted) {
  $seed = if ($null -eq $r.Seed) { '' } else { $r.Seed }
  $p = if ($null -eq $r.Policies) { '' } else { $r.Policies }
  $q = if ($null -eq $r.Queries) { '' } else { $r.Queries }
  $st = if ($null -eq $r.Stage) { '' } else { "{0:00}" -f [int]$r.Stage }
  $cs = if ($null -eq $r.CandidateScore) { '' } else { "{0:N6}" -f [double]$r.CandidateScore }
  $lines.Add(("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} |" -f $seed, $p, $q, $st, $r.Decision, $cs, $r.CandidateRunId, $r.ActiveRunId)) | Out-Null
}

Set-Content -LiteralPath $mdPath -Value ($lines -join "`r`n") -Encoding UTF8

Write-Host ""
Write-Host "[PosNegSummary] Wrote:"
Write-Host "  $csvPath"
Write-Host "  $mdPath"
