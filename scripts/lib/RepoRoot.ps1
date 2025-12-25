# PowerShell helper functions to locate the repository root and build repo-relative paths.
# Intended to be dot-sourced by scripts under .\scripts\*

function Get-RepoRoot {
  [CmdletBinding()]
  param(
    [string]$StartPath = $PSScriptRoot,
    [int]$MaxDepth = 8
  )

  if ([string]::IsNullOrWhiteSpace($StartPath)) {
    throw "Get-RepoRoot: StartPath is empty."
  }

  $resolved = Resolve-Path -LiteralPath $StartPath -ErrorAction Stop
  $dir = $resolved.Path

  if (Test-Path -LiteralPath $dir -PathType Leaf) {
    $dir = Split-Path -LiteralPath $dir -Parent
  }

  for ($i = 0; $i -le $MaxDepth; $i++) {
    $hasSln = Test-Path -LiteralPath (Join-Path $dir "EmbeddingShift.sln")
    $hasSrc = Test-Path -LiteralPath (Join-Path $dir "src")
    if ($hasSln -and $hasSrc) {
      return (Resolve-Path -LiteralPath $dir).Path
    }

    $parent = Split-Path -LiteralPath $dir -Parent
    if ([string]::IsNullOrWhiteSpace($parent) -or ($parent -eq $dir)) {
      break
    }
    $dir = $parent
  }

  throw "Repository root not found. Expected to find 'EmbeddingShift.sln' and 'src' in a parent directory. StartPath=$StartPath"
}

function Join-RepoPath {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory=$true)][string]$RepoRoot,
    [Parameter(Mandatory=$true)][string[]]$Parts
  )

  $p = $RepoRoot
  foreach ($part in $Parts) {
    $p = Join-Path -Path $p -ChildPath $part
  }
  return $p
}

function Assert-File {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory=$true)][string]$Path,
    [string]$Label = "File"
  )
  if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "$Label not found: $Path"
  }
}

function Assert-Dir {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory=$true)][string]$Path,
    [string]$Label = "Directory"
  )
  if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
    throw "$Label not found: $Path"
  }
}
