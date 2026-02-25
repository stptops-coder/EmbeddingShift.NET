Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot([string]$from) {
  return (Resolve-Path (Join-Path $from "..\..")).Path
}

$RepoRoot = Resolve-RepoRoot $PSScriptRoot
Set-Location $RepoRoot

.\scripts\runbook-experimental\07-Clean-TestScratch-TestsData.ps1
.\scripts\runbook-experimental\08-Clean-Temp-EmbeddingShiftTests.ps1

Write-Host "[VS-AfterTests] Done."
