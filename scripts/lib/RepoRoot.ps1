<#
Utility helpers to resolve the repository root folder.

Notes:
- Keep this file dependency-free (no module imports).
- Scripts should prefer resolving RepoRoot relative to their own location,
  but this helper is handy when the starting path varies.
#>

function Resolve-RepoRoot {
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$StartPath = (Get-Location).Path
    )

    $resolved = Resolve-Path -LiteralPath $StartPath -ErrorAction Stop
    $path = $resolved.Path

    if (Test-Path -LiteralPath $path -PathType Leaf) {
        $path = Split-Path -Parent $path
    }

    while ($true) {
        if (Test-Path -LiteralPath (Join-Path $path '.git') -PathType Container) {
            return $path
        }

        $sln = Get-ChildItem -LiteralPath $path -Filter *.sln -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $sln) {
            return $path
        }

        if (Test-Path -LiteralPath (Join-Path $path 'src') -PathType Container) {
            return $path
        }

        $parent = Split-Path -Parent $path
        if ([string]::IsNullOrWhiteSpace($parent) -or ($parent -eq $path)) {
            throw "Repository root not found starting from: $StartPath"
        }

        $path = $parent
    }
}

function Get-RepoRoot {
    [CmdletBinding()]
    param(
        [Parameter()]
        [string]$StartPath = (Get-Location).Path
    )

    return Resolve-RepoRoot -StartPath $StartPath
}
