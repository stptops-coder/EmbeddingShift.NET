Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot([string]$from) {
  return (Resolve-Path (Join-Path $from "..\..")).Path
}

$RepoRoot = Resolve-RepoRoot $PSScriptRoot
Set-Location $RepoRoot

.\scripts\runbook-experimental\05-VS-CleanEmbeddingShiftEnv.ps1

$vs = Get-Process devenv -ErrorAction SilentlyContinue
if ($vs) {
  Write-Host "[VS-LaunchClean] Visual Studio (devenv) is running. Please close it and re-run this script."
  exit 2
}

$sln = Join-Path $RepoRoot "EmbeddingShift.sln"
if (-not (Test-Path $sln)) { throw "Solution not found: $sln" }

Write-Host "[VS-LaunchClean] Starting Visual Studio: $sln"
Start-Process -FilePath $sln | Out-Null
