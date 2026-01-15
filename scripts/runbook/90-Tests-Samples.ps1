Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $RepoRoot

# Ensure tests do NOT accidentally use a generated dataset
Remove-Item Env:\EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT -ErrorAction SilentlyContinue
$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = (Join-Path $RepoRoot "samples\insurance")

if (-not (Test-Path $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT)) {
  throw "Samples dataset root not found: $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT"
}

Write-Host ("[Tests] DATASET_ROOT=" + $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT)

dotnet test ".\src\EmbeddingShift.Tests\EmbeddingShift.Tests.csproj"
