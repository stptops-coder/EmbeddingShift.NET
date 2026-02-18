[CmdletBinding()]
param(
    [int]$Stages = 3,
    [int]$Seed = 1006,
    [ValidateSet('deterministic','stochastic')][string]$SimMode = 'deterministic',
    [string]$Tenant = 'insurer-a',
    [string]$Domain = 'mini-insurance',
    [string]$ResultsDomain = 'insurance',
    [switch]$Overwrite
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$runScript = Join-Path $RepoRoot 'scripts\run\Run-FirstLight-MultiStage.ps1'
$inspectScript = Join-Path $RepoRoot 'scripts\inspect\Inspect-RunRoot.ps1'
$healthScript = Join-Path $RepoRoot 'scripts\inspect\Inspect-RunRootHealth.ps1'

Write-Host "[Run] $runScript"

# Multi-stage runner returns the RunRoot via pipeline output.
# IMPORTANT: dotnet wrapper output can pollute the pipeline; therefore we only take the last output item as the RunRoot.
$runRootOut = & $runScript -Stages $Stages -Seed $Seed -SimMode $SimMode -Tenant $Tenant -Domain $Domain -ResultsDomain $ResultsDomain -Overwrite:$Overwrite

$runRoot = ($runRootOut | Where-Object { $_ -is [string] -and $_ -match '^[A-Za-z]:\\' } | Select-Object -Last 1)

if ([string]::IsNullOrWhiteSpace($runRoot)) {
    throw "Run-FirstLight-MultiStage.ps1 did not return a RunRoot path. Output: $runRootOut"
}

$runRoot = (Resolve-Path -LiteralPath $runRoot).Path

Write-Host "[RunRoot] $runRoot"

# Persist for follow-up runbook scripts in the current PowerShell session.
$env:EMBEDDINGSHIFT_ROOT = $runRoot
$env:EMBEDDINGSHIFT_RESULTS_DOMAIN = $ResultsDomain
$env:EMBEDDINGSHIFT_TENANT = $Tenant
$env:EMBEDDINGSHIFT_LAYOUT = 'tenant'

$datasetName = ("FirstLight{0}-{1}" -f $Stages, $Seed)
$datasetStage0 = Join-Path (Join-Path (Join-Path (Join-Path (Join-Path (Join-Path $runRoot "results") $ResultsDomain) "tenants") $Tenant) "datasets") $datasetName
$datasetStage0 = Join-Path $datasetStage0 "stage-00"
$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $datasetStage0

# Make RunRoot easy to capture: return it via pipeline output.
# (Callers often do: $runRoot = .\scripts\run\Run-FirstLight-EndToEnd.ps1 ...)
Write-Output $runRoot

# Summary report (best-effort)
try {
    Write-Host "[Summary] runroot-summarize"
    & dotnet run --project (Join-Path $RepoRoot 'src\EmbeddingShift.ConsoleEval') -- domain $Domain runroot-summarize --runroot=$runRoot | Out-Host
}
catch {
    Write-Warning "runroot-summarize failed: $($_.Exception.Message)"
}

# Health report (best-effort)
try {
    & $healthScript -RunRoot $runRoot -Domain $ResultsDomain -Tenant $Tenant -WriteReport | Out-Host
}
catch {
    $pos = $null
    try { $pos = $_.InvocationInfo.PositionMessage } catch { }
    if ([string]::IsNullOrWhiteSpace($pos)) {
        Write-Warning ("Inspect-RunRootHealth failed: {0}" -f $_.Exception.Message)
    } else {
        Write-Warning ("Inspect-RunRootHealth failed: {0}`n{1}" -f $_.Exception.Message, $pos)
    }

}
# Index JSON for quick navigation (best-effort)
try {
    & $inspectScript -RunRoot $runRoot -Domain $ResultsDomain -Tenant $Tenant -WriteJsonIndex | Out-Host
}
catch {
    Write-Warning "Inspect-RunRoot failed: $($_.Exception.Message)"
}
