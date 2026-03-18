Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $RepoRoot

Write-Host "[Build] Solution build..."
dotnet build

$segmenterProject = Join-Path $RepoRoot "private\SegmenterTool\SegmenterTool\SegmenterTool.csproj"
if (Test-Path -LiteralPath $segmenterProject) {
    Write-Host "[Build] SegmenterTool build..."
    dotnet build $segmenterProject
}
else {
    Write-Host "[Build] SegmenterTool build skipped (optional external/private dependency not present)."
}
