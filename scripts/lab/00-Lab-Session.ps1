Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
Lab session bootstrap for command-by-command debugging.

Goal:
- Start from a clean, isolated tenant
- Optionally isolate DATA_ROOT to avoid mixing with other runs
- Provide Snap/Diff helpers to observe exactly which files/dirs are created per command

Notes:
- All comments and outputs are English (repo convention).
- This script does NOT run the CLI commands for you. It just prepares the environment and helpers.
#>

param(
    [string]$Tenant = ("lab-{0}" -f (Get-Date -Format "yyyyMMdd-HHmm") ),
    [switch]$UseIsolatedDataRoot = $true,
    [switch]$BlankField = $true,
    [string]$DatasetRoot = ""
)

function Write-Section([string]$Title) {
    Write-Host ""
    Write-Host ("=== {0} ===" -f $Title)
}

# Resolve repo root and switch to it (safe to run from anywhere)
$repoRoot = & (Join-Path $PSScriptRoot "..\lib\RepoRoot.ps1")
Set-Location -LiteralPath $repoRoot

# Default dataset root = samples/insurance (repo-relative)
if ([string]::IsNullOrWhiteSpace($DatasetRoot)) {
    $DatasetRoot = Join-Path $repoRoot "samples\insurance"
}

# 1) Clear all EMBEDDINGSHIFT_* variables to avoid mixing runs
Get-ChildItem Env:EMBEDDINGSHIFT_* -ErrorAction SilentlyContinue | Remove-Item -ErrorAction SilentlyContinue

# 2) Set tenant + dataset root
$env:EMBEDDINGSHIFT_TENANT = $Tenant
$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $DatasetRoot

# 3) Optionally isolate DATA_ROOT (recommended for lab runs)
if ($UseIsolatedDataRoot) {
    $env:EMBEDDINGSHIFT_DATA_ROOT = Join-Path $repoRoot ("data\_lab\{0}" -f $Tenant)
} else {
    # Let the app fall back to default repoRoot\data
    Remove-Item Env:EMBEDDINGSHIFT_DATA_ROOT -ErrorAction SilentlyContinue
}

# 4) Optional blank field: delete only the lab roots for this tenant
$resultsTenantRoot = Join-Path $repoRoot ("results\insurance\tenants\{0}" -f $Tenant)

if ($BlankField) {
    if ($UseIsolatedDataRoot -and (Test-Path -LiteralPath $env:EMBEDDINGSHIFT_DATA_ROOT)) {
        Remove-Item -LiteralPath $env:EMBEDDINGSHIFT_DATA_ROOT -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $resultsTenantRoot) {
        Remove-Item -LiteralPath $resultsTenantRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# 5) Snapshot helpers (dirs + files)
function Snap([string]$Label) {
    $resultsRoot = Join-Path $repoRoot "results"
    $tenantKey = $env:EMBEDDINGSHIFT_TENANT

    $dataRoot = $env:EMBEDDINGSHIFT_DATA_ROOT
    if ([string]::IsNullOrWhiteSpace($dataRoot)) { $dataRoot = Join-Path $repoRoot "data" }

    $paths = @(
        (Join-Path $resultsRoot "_scratch"),
        (Join-Path $resultsRoot ("insurance\tenants\{0}" -f $tenantKey)),
        (Join-Path $dataRoot "embeddings\MiniInsurance"),
        (Join-Path $dataRoot "manifests\MiniInsurance")
    )

    $outDir = Join-Path $resultsRoot "_scratch\_snapshots"
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    $out = Join-Path $outDir ("{0}_{1}.txt" -f (Get-Date -Format "yyyyMMdd_HHmmss"), $Label)

    $lines = foreach ($p in $paths) {
        if (Test-Path -LiteralPath $p) {
            # Include both directories and files, stable ordering
            Get-ChildItem -LiteralPath $p -Force -Recurse -ErrorAction SilentlyContinue |
                Sort-Object FullName |
                ForEach-Object {
                    if ($_.PSIsContainer) {
                        "D|{0}" -f $_.FullName
                    } else {
                        # Use UTC to avoid local timezone drift in comparisons
                        $utc = $_.LastWriteTimeUtc.ToString("o")
                        "F|{0}|{1}|{2}" -f $_.Length, $utc, $_.FullName
                    }
                }
        } else {
            "[missing] $p"
        }
    }

    $lines | Set-Content -LiteralPath $out -Encoding utf8
    Write-Host ("[Snap] {0}" -f $out)
    return $out
}

function Diff([string]$Prev, [string]$Now) {
    Compare-Object (Get-Content -LiteralPath $Prev) (Get-Content -LiteralPath $Now) |
        Where-Object { $_.SideIndicator -eq "=>" } |
        Select-Object -ExpandProperty InputObject
}

function Show-LabEnv() {
    $dataRoot = $env:EMBEDDINGSHIFT_DATA_ROOT
    if ([string]::IsNullOrWhiteSpace($dataRoot)) { $dataRoot = Join-Path $repoRoot "data" }

    Write-Host ("RepoRoot                       = {0}" -f $repoRoot)
    Write-Host ("EMBEDDINGSHIFT_TENANT           = {0}" -f $env:EMBEDDINGSHIFT_TENANT)
    Write-Host ("EMBEDDINGSHIFT_DATA_ROOT        = {0}" -f $dataRoot)
    Write-Host ("EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = {0}" -f $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT)
}

Write-Section "Lab session prepared"
Show-LabEnv

Write-Host ""
Write-Host "Recommended command-by-command sequence:"
Write-Host "  $S0 = Snap '00_blank'"
Write-Host "  dotnet run --project src/EmbeddingShift.ConsoleEval -- dataset-status MiniInsurance --role=all"
Write-Host "  $S1 = Snap '01_after_status' ; Diff $S0 $S1"
Write-Host "  dotnet run --project src/EmbeddingShift.ConsoleEval -- dataset-reset MiniInsurance --role=all --force"
Write-Host "  dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-dataset samples/insurance/policies samples/insurance/queries/queries.json MiniInsurance"
Write-Host "  dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-inspect MiniInsurance --role=refs"
Write-Host "  dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-inspect MiniInsurance --role=queries"
Write-Host "  dotnet run --project src/EmbeddingShift.ConsoleEval -- dataset-validate MiniInsurance --role=all"
Write-Host ""
Write-Host "Tip: If you want to keep everything inside DATA_ROOT, make sure UseIsolatedDataRoot is ON (default)."
