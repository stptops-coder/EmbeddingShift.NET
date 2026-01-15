Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# RepoRoot = two levels up from scripts\runbook
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $RepoRoot

Write-Host "[Prep] RepoRoot = $RepoRoot"

# Ensure folders exist
$privateDir = Join-Path $RepoRoot "private"
if (-not (Test-Path $privateDir)) { New-Item -ItemType Directory -Path $privateDir | Out-Null }

# Avoid the classic footgun: tests / runs accidentally pick up a stale dataset root
Remove-Item Env:\EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT -ErrorAction SilentlyContinue

# Optional: show dotnet + git quick sanity
Write-Host ("[Prep] dotnet = " + (& dotnet --version))
Write-Host ("[Prep] git status:")
& git status
