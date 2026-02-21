Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Deletes scratch run folders while preserving the shared scratch active pointer under:
#   results\_scratch\_active\...
#
# This is useful when you want "greenfield" scratch runs but still keep the active baseline
# for runs-decide/runs-promote across scratch sweeps.

param(
    [string]$ScratchRoot
)

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $ScratchRoot -or $ScratchRoot.Trim().Length -eq 0) {
    $ScratchRoot = Join-Path $RepoRoot 'results\_scratch'
} else {
    $ScratchRoot = (Resolve-Path $ScratchRoot).Path
}

if (-not (Test-Path $ScratchRoot)) {
    Write-Host "[CleanScratch] Scratch root does not exist: $ScratchRoot"
    exit 0
}

Write-Host "[CleanScratch] RepoRoot   = $RepoRoot"
Write-Host "[CleanScratch] ScratchRoot= $ScratchRoot"
Write-Host "[CleanScratch] Preserving : _active"

$items = Get-ChildItem -LiteralPath $ScratchRoot -Force -ErrorAction Stop
$toDelete = $items | Where-Object { $_.PSIsContainer -and $_.Name -ne '_active' }

if ($toDelete.Count -eq 0) {
    Write-Host "[CleanScratch] Nothing to delete."
    exit 0
}

foreach ($d in $toDelete) {
    Write-Host "[CleanScratch] Deleting: $($d.FullName)"
    Remove-Item -LiteralPath $d.FullName -Recurse -Force -ErrorAction Stop
}

Write-Host "[CleanScratch] Done."
