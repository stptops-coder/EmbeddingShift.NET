Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot([string]$from) {
  return (Resolve-Path (Join-Path $from "..\..")).Path
}

$RepoRoot = Resolve-RepoRoot $PSScriptRoot
Set-Location $RepoRoot

$testsData = Join-Path $RepoRoot "results\_scratch\tests-data"
if (Test-Path $testsData) {
  Write-Host "[Clean] Removing $testsData"
  Remove-Item -Recurse -Force -LiteralPath $testsData
}
New-Item -ItemType Directory -Force -Path $testsData | Out-Null
Write-Host "[Clean] OK: tests-data is empty."
