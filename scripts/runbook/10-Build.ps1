Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $RepoRoot

Write-Host "[Build] Solution build..."
dotnet build

Write-Host "[Build] SegmenterTool build..."
dotnet build ".\private\SegmenterTool\SegmenterTool\SegmenterTool.csproj"
