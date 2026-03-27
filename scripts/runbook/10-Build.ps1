Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '..\lib\RepoRoot.ps1')
. (Join-Path $PSScriptRoot '..\lib\DotNet.ps1')

$RepoRoot = Resolve-RepoRoot -StartPath $PSScriptRoot
Set-Location $RepoRoot

Write-Host "[Build] Solution build..."
Invoke-DotNet -Args @('build') -WorkingDirectory $RepoRoot | Out-Null

$segmenterProject = Join-Path $RepoRoot "private\SegmenterTool\SegmenterTool\SegmenterTool.csproj"
if (Test-Path -LiteralPath $segmenterProject) {
    Write-Host "[Build] SegmenterTool build..."
    Invoke-DotNet -Args @('build', $segmenterProject) -WorkingDirectory $RepoRoot | Out-Null
}
else {
    Write-Host "[Build] SegmenterTool build skipped (optional external/private dependency not present)."
}
