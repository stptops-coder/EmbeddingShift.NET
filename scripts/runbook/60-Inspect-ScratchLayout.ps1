<# 
Inspect the layout under results\_scratch for a given "scenario" folder and the latest run root.

Usage examples:

  # Auto-select the most recent scenario under results\_scratch and inspect its latest run root
  .\scripts\runbook\60-Inspect-ScratchLayout.ps1

  # Inspect a specific scenario (folder under results\_scratch), auto-select latest run root
  .\scripts\runbook\60-Inspect-ScratchLayout.ps1 -Scenario "EmbeddingShift.FirstLight"

  # Inspect an explicit run root
  .\scripts\runbook\60-Inspect-ScratchLayout.ps1 -Root "C:\pg\RakeX\results\_scratch\EmbeddingShift.MiniInsurance\20260217_144550"
#>

[CmdletBinding()]
param(
  [string]$Root = '',
  [string]$Scenario = '',
  [string]$ResultsRoot = '',
  [int]$Top = 40
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot([string]$explicit) {
  if (-not [string]::IsNullOrWhiteSpace($explicit)) { return $explicit }
  return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
}

function Get-LatestChildDir([string]$parent) {
  if (-not (Test-Path -LiteralPath $parent -PathType Container)) { return $null }
  return (Get-ChildItem -LiteralPath $parent -Directory |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName)
}

$RepoRoot = Get-RepoRoot $env:EMBEDDINGSHIFT_REPO_ROOT
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

if ([string]::IsNullOrWhiteSpace($ResultsRoot)) {
  $ResultsRoot = Join-Path $RepoRoot "results"
}
$ResultsRoot = (Resolve-Path -LiteralPath $ResultsRoot).Path

$ScratchBase = Join-Path $ResultsRoot "_scratch"
if (-not (Test-Path -LiteralPath $ScratchBase -PathType Container)) {
  throw "Scratch folder not found: $ScratchBase"
}

if ([string]::IsNullOrWhiteSpace($Scenario)) {
  # If an explicit run root is provided, derive the scenario from the path to avoid misleading output.
  if (-not [string]::IsNullOrWhiteSpace($Root)) {
    try {
      $resolvedRoot = (Resolve-Path -LiteralPath $Root -ErrorAction Stop).Path
      if ($resolvedRoot.StartsWith($ScratchBase, [System.StringComparison]::OrdinalIgnoreCase)) {
        $rel = $resolvedRoot.Substring($ScratchBase.Length).TrimStart('\\')
        $parts = $rel.Split('\\')
        if ($parts.Length -ge 1 -and -not [string]::IsNullOrWhiteSpace($parts[0])) {
          $Scenario = $parts[0]
          Write-Host ("[Inspect] Derived Scenario from -Root = {0}" -f $Scenario)
        }
      }
    } catch {
      # ignore - we'll fall back to auto-selection below
    }
  }

  if ([string]::IsNullOrWhiteSpace($Scenario)) {
    $Scenario = (Get-ChildItem -LiteralPath $ScratchBase -Directory -ErrorAction SilentlyContinue |
      Sort-Object LastWriteTime -Descending |
      Select-Object -First 1 -ExpandProperty Name)
    if ([string]::IsNullOrWhiteSpace($Scenario)) {
      Write-Warning "No scratch scenario folders found under: $ScratchBase"
      Write-Host "Hint: run a scenario first (e.g. .\\scripts\\runbook\\99-RunAll.ps1) or pass -Scenario / -Root."
      return
    }
    Write-Host ("[Inspect] Auto-selected Scenario = {0}" -f $Scenario)
  }
}


$ScratchRoot = Join-Path $ScratchBase $Scenario
if (-not (Test-Path -LiteralPath $ScratchRoot -PathType Container)) {
  throw "Scenario folder not found: $ScratchRoot"
}

if ([string]::IsNullOrWhiteSpace($Root)) {
  $Root = Get-LatestChildDir $ScratchRoot
}

if ([string]::IsNullOrWhiteSpace($Root)) {
  throw "No run root found under: $ScratchRoot"
}

$Root = (Resolve-Path -LiteralPath $Root).Path

Write-Host "=== Scratch Layout Inspector ==="
Write-Host ("RepoRoot    : {0}" -f $RepoRoot)
Write-Host ("ResultsRoot : {0}" -f $ResultsRoot)
Write-Host ("Scenario    : {0}" -f $Scenario)
Write-Host ("RunRoot     : {0}" -f $Root)
Write-Host ""

Write-Host "Top-level:"
Get-ChildItem -LiteralPath $Root -Force | Select-Object -First $Top Name, Mode, Length, LastWriteTime | Format-Table -AutoSize

Write-Host ""
Write-Host "Recursive (depth 3) folder map:"
Get-ChildItem -LiteralPath $Root -Recurse -Depth 3 -Directory -Force |
  Sort-Object FullName |
  ForEach-Object {
    $rel = $_.FullName.Substring($Root.Length).TrimStart('\')
    if ($rel -eq '') { $rel = '.' }
    "[d] $rel"
  } | Select-Object -First $Top

Write-Host ""
Write-Host "Hint:"
Write-Host "  - Use -Scenario <foldername> to target a specific scratch scenario."
Write-Host "  - Use -Root <full path> to inspect a specific run root."
