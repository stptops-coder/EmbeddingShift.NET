Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

# Prefer the current process scope. If unset, try to discover the latest scratch run root.
$RunRoot = $env:EMBEDDINGSHIFT_ROOT
if ([string]::IsNullOrWhiteSpace($RunRoot)) {
    $scratchBase = Join-Path $RepoRoot 'results\_scratch\EmbeddingShift.MiniInsurance'
    if (Test-Path $scratchBase) {
        $latest = Get-ChildItem -Path $scratchBase -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($latest) { $RunRoot = $latest.FullName }
    }
}

$Tenant = $env:EMBEDDINGSHIFT_TENANT
if ([string]::IsNullOrWhiteSpace($Tenant)) { $Tenant = 'insurer-a' }

$DatasetRoot = $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT

Write-Host "[Health] RepoRoot = $RepoRoot"
Write-Host ("[Health] RunRoot  = " + ($(if ($RunRoot) { $RunRoot } else { '<none>' })))
Write-Host "[Health] Tenant   = $Tenant"
Write-Host ("[Health] Dataset  = " + ($(if ($DatasetRoot) { $DatasetRoot } else { '<none>' })))

# Try to infer dataset root if missing but we have a RunRoot
if ([string]::IsNullOrWhiteSpace($DatasetRoot) -and $RunRoot) {
    $datasetsBase = Join-Path $RunRoot "results\insurance\tenants\$Tenant\datasets"
    if (Test-Path $datasetsBase) {
        $stage0 = Get-ChildItem -Path $datasetsBase -Recurse -Directory -Filter 'stage-00' -ErrorAction SilentlyContinue |
                 Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($stage0) { $DatasetRoot = $stage0.FullName }
    }
}

# Basic dataset sanity
if ($DatasetRoot -and (Test-Path (Join-Path $DatasetRoot 'policies'))) {
    Write-Host "[Health] policies : OK"
} elseif ($DatasetRoot) {
    Write-Host "[Health] policies : MISSING"
}

# Find latest compare/decision reports
function Get-LatestFile([string]$dir, [string]$filter) {
    if (-not (Test-Path $dir)) { return $null }
    return Get-ChildItem -Path $dir -File -Filter $filter -ErrorAction SilentlyContinue |
           Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

$compare = $null
$decision = $null

if ($RunRoot) {
    $runsRoot = Join-Path $RunRoot "results\insurance\tenants\$Tenant\runs"
    $compare = Get-LatestFile -dir (Join-Path $runsRoot '_compare') -filter 'compare_*.md'
$decideRoot = Join-Path $runsRoot '_decide'
$decisionsRoot = Join-Path $runsRoot '_decisions'
if (Test-Path $decisionsRoot) { $decideRoot = $decisionsRoot }
$decision = Get-LatestFile -dir $decideRoot -filter 'decision_*.md'
}

Write-Host ("[Health] compare  : " + ($(if ($compare) { $compare.FullName } else { '<none>' })))
Write-Host ("[Health] decision : " + ($(if ($decision) { $decision.FullName } else { '<none>' })))
