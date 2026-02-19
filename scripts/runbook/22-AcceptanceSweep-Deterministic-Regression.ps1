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

    [string]$Domain  = 'insurance',
    [string]$Tenant  = 'insurer-a',
    [string]$MetricKey = 'ndcg@3'
)

function Get-SafeFileName([string]$name) {
    $invalid = [IO.Path]::GetInvalidFileNameChars()
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $name.ToCharArray()) {
        if ($invalid -contains $ch) { [void]$sb.Append('_') } else { [void]$sb.Append($ch) }
    }
    return $sb.ToString()
}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$SweepScript = Join-Path $PSScriptRoot '21-AcceptanceSweep-Deterministic.ps1'

if (-not (Test-Path $SweepScript)) {
    throw "Sweep script not found: $SweepScript"
}

$safeMetric = Get-SafeFileName $MetricKey
$sharedActivePath = Join-Path $RepoRoot ("results\_scratch\_active\{0}\tenants\{1}\runs\_active\active_{2}.json" -f $Domain, $Tenant, $safeMetric)

Write-Host "[Regression] RepoRoot        = $RepoRoot"
Write-Host "[Regression] SweepScript     = $SweepScript"
Write-Host "[Regression] SharedActive    = $sharedActivePath"
Write-Host "[Regression] Params          = RootMode=$RootMode, Policies=$($Policies -join ','), Queries=$($Queries -join ','), Stages=$Stages, Seed=$Seed, MetricKey=$MetricKey"

Write-Host ""
Write-Host "============================================================"
Write-Host "[Regression] Run #1 (expected: may Promote if no active yet)"
Write-Host "============================================================"

& $SweepScript -RootMode $RootMode -Policies $Policies -Queries $Queries -Stages $Stages -Seed $Seed -Promote

if (-not (Test-Path $sharedActivePath)) {
    throw "Shared active pointer was not created: $sharedActivePath"
}

$before = Get-Content -LiteralPath $sharedActivePath -Raw -ErrorAction Stop

Write-Host ""
Write-Host "============================================================"
Write-Host "[Regression] Run #2 (expected: KeepActive, shared active unchanged)"
Write-Host "============================================================"

& $SweepScript -RootMode $RootMode -Policies $Policies -Queries $Queries -Stages $Stages -Seed $Seed -Promote

$after = Get-Content -LiteralPath $sharedActivePath -Raw -ErrorAction Stop

if ($before -ne $after) {
    Write-Host "[Regression] FAIL: Shared active changed after 2nd run."
    Write-Host "Hint: If you changed simulation mode / enabled noise, this is expected."
    exit 2
}

Write-Host "[Regression] PASS: Shared active unchanged after 2nd run."
