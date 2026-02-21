Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Sets a coherent set of environment variables for running the CLI.
# This script is intentionally conservative: it applies defaults that match the repo layout,
# but still allows explicit overrides via parameters.

param(
    [Parameter(Mandatory = $false)]
    [string] $ResultsRoot = "",

    [Parameter(Mandatory = $false)]
    [string] $DataRoot = "",

    [Parameter(Mandatory = $false)]
    [string] $Root = "",

    [Parameter(Mandatory = $false)]
    [string] $DatasetRoot = "",

    [Parameter(Mandatory = $false)]
    [string] $Tenant = "",

    [Parameter(Mandatory = $false)]
    [ValidateSet("scratch", "tenant")]
    [string] $Layout = "scratch",

    [Parameter(Mandatory = $false)]
    [string] $Domain = "insurance",

    [Parameter(Mandatory = $false)]
    [switch] $Force
)

. "$PSScriptRoot\..\lib\RepoRoot.ps1"

# Defaults
if ([string]::IsNullOrWhiteSpace($ResultsRoot)) { $ResultsRoot = Join-Path $RepoRoot "results" }
if ([string]::IsNullOrWhiteSpace($DataRoot))    { $DataRoot = Join-Path $RepoRoot "data" }
if ([string]::IsNullOrWhiteSpace($Root))        { $Root = Join-Path $ResultsRoot "_scratch" }
if ([string]::IsNullOrWhiteSpace($DatasetRoot)) { $DatasetRoot = Join-Path $RepoRoot "samples\insurance" }

# Apply core roots
if ($Force -or -not [string]::IsNullOrWhiteSpace($ResultsRoot)) { $env:EMBEDDINGSHIFT_RESULTS_ROOT = $ResultsRoot }
if ($Force -or -not [string]::IsNullOrWhiteSpace($DataRoot))    { $env:EMBEDDINGSHIFT_DATA_ROOT = $DataRoot }
if ($Force -or -not [string]::IsNullOrWhiteSpace($Root))        { $env:EMBEDDINGSHIFT_ROOT = $Root }
if ($Force -or -not [string]::IsNullOrWhiteSpace($DatasetRoot)) { $env:EMBEDDINGSHIFT_DATASET_ROOT = $DatasetRoot }

# Keep domain/layout in sync with how the CLI will resolve paths
$env:EMBEDDINGSHIFT_RESULTS_DOMAIN = $Domain
$env:EMBEDDINGSHIFT_LAYOUT = $Layout

if ($Layout -eq 'tenant') {
    if ([string]::IsNullOrWhiteSpace($Tenant)) {
        throw "Tenant is required when Layout='tenant'."
    }
    $env:EMBEDDINGSHIFT_TENANT = $Tenant
}
else {
    # Avoid stale values from previous runs.
    $env:EMBEDDINGSHIFT_TENANT = ""
}

Write-Host "[Paths] RepoRoot=$RepoRoot"
Write-Host "[Paths] ResultsRoot=$ResultsRoot"
Write-Host "[Paths] DataRoot=$DataRoot"
Write-Host "[Paths] Root=$Root"
Write-Host "[Paths] DatasetRoot=$DatasetRoot"
Write-Host "[Paths] Domain=$Domain"
Write-Host "[Paths] Layout=$Layout"

if ($Layout -eq 'tenant') {
    Write-Host "[Paths] Tenant=$Tenant"
}

[PSCustomObject]@{
    RepoRoot     = $RepoRoot
    ResultsRoot  = $ResultsRoot
    DataRoot     = $DataRoot
    Root         = $Root
    DatasetRoot  = $DatasetRoot
    Domain       = $Domain
    Layout       = $Layout
    Tenant       = if ($Layout -eq 'tenant') { $Tenant } else { "" }
}
