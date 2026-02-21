Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Regression helper for scratch acceptance sweeps:
# - Runs 21-AcceptanceSweep-Deterministic twice with the same parameters (-Promote).
# - Asserts that the shared scratch active pointer file stays unchanged after the 2nd run,
#   i.e. the decision was KeepActive and promotion was skipped.
#
# NOTE:
# This relies on deterministic simulation. If you change backend or enable noise, this
# check is not meaningful.

param(
    [ValidateSet('scratch')]
    [string]$RootMode = 'scratch',

    [int[]]$Policies = @(40),
    [int[]]$Queries  = @(80),
    [int]$Stages     = 2,
    [int]$Seed       = 1337,

    [string]$DsName = 'SweepDS',

    [string]$SimAlgo = 'sha256',
    [int]$SimSemanticCharNGrams = 3,

    [string]$Domain  = 'insurance',
    [string]$Tenant  = 'insurer-a',
    [string]$MetricKey = 'ndcg@3'
)

if ($Policies.Count -ne 1 -or $Queries.Count -ne 1) {
    throw "This regression helper expects exactly one Policies value and one Queries value."
}

function Get-ProfileKey {
    param(
        [string]$Backend,
        [string]$SimMode,
        [string]$SimAlgo,
        [int]$SimSemanticCharNGrams,
        [string]$DsName,
        [int]$Seed,
        [int]$Stages,
        [int]$Policies,
        [int]$Queries
    )

    $k = "{0}_{1}__{2}__ng{3}__ds{4}__seed{5}__st{6}__p{7}__q{8}" -f $Backend, $SimMode, $SimAlgo, $SimSemanticCharNGrams, $DsName, $Seed, $Stages, $Policies, $Queries
    return ($k -replace '[^a-zA-Z0-9_\-\.]+', '_')
}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$SweepScript = Join-Path (Join-Path $PSScriptRoot '..\runbook') '21-AcceptanceSweep-Deterministic.ps1'

if (-not (Test-Path $SweepScript)) {
    throw "Sweep script not found: $SweepScript"
}

$backend = 'sim'
$simMode = 'deterministic'
$p = $Policies[0]
$q = $Queries[0]
$profileKey = Get-ProfileKey -Backend $backend -SimMode $simMode -SimAlgo $SimAlgo -SimSemanticCharNGrams $SimSemanticCharNGrams -DsName $DsName -Seed $Seed -Stages $Stages -Policies $p -Queries $q
$sharedActiveDir = Join-Path $RepoRoot ("results\_scratch\_active\{0}\tenants\{1}\runs\_active\profiles\{2}" -f $Domain, $Tenant, $profileKey)
$sharedActivePath = Join-Path $sharedActiveDir ("active_{0}.json" -f $MetricKey)

Write-Host "[Regression] RepoRoot        = $RepoRoot"
Write-Host "[Regression] SweepScript     = $SweepScript"
Write-Host "[Regression] SharedActive    = $sharedActivePath"
Write-Host "[Regression] Params          = RootMode=$RootMode, Policies=$p, Queries=$q, Stages=$Stages, Seed=$Seed, DsName=$DsName, SimAlgo=$SimAlgo, SimSemanticCharNGrams=$SimSemanticCharNGrams, MetricKey=$MetricKey"

Write-Host ""
Write-Host "============================================================"
Write-Host "[Regression] Run #1 (expected: may Promote if no active yet)"
Write-Host "============================================================"

& $SweepScript -RootMode $RootMode -Policies $Policies -Queries $Queries -Stages $Stages -Seed $Seed -DsName $DsName -SimAlgo $SimAlgo -SimSemanticCharNGrams $SimSemanticCharNGrams -Metric $MetricKey -Promote

if (-not (Test-Path $sharedActivePath)) {
    throw "Shared active pointer was not created: $sharedActivePath"
}

$before = Get-Content -LiteralPath $sharedActivePath -Raw -ErrorAction Stop

Write-Host ""
Write-Host "============================================================"
Write-Host "[Regression] Run #2 (expected: KeepActive, shared active unchanged)"
Write-Host "============================================================"

& $SweepScript -RootMode $RootMode -Policies $Policies -Queries $Queries -Stages $Stages -Seed $Seed -DsName $DsName -SimAlgo $SimAlgo -SimSemanticCharNGrams $SimSemanticCharNGrams -Metric $MetricKey -Promote

$after = Get-Content -LiteralPath $sharedActivePath -Raw -ErrorAction Stop

if ($before -ne $after) {
    Write-Host "[Regression] FAIL: Shared active changed after 2nd run."
    Write-Host "Hint: If you changed simulation mode / enabled noise, this is expected."
    exit 2
}

Write-Host "[Regression] PASS: Shared active unchanged after 2nd run."
