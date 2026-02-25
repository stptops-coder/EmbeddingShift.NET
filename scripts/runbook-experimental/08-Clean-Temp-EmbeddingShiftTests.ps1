Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot([string]$from) {
  return (Resolve-Path (Join-Path $from "..\..")).Path
}

$repoRoot = Resolve-RepoRoot $PSScriptRoot
Set-Location $repoRoot

$temp = [System.IO.Path]::GetTempPath()
$targets = @(
  (Join-Path $temp "EmbeddingShift.Tests"),
  (Join-Path $temp "EmbeddingShift.Acceptance")
)

foreach ($t in $targets) {
  if (Test-Path $t) {
    Write-Host "[Clean] Removing $t"
    Remove-Item -Recurse -Force -LiteralPath $t -ErrorAction SilentlyContinue
  } else {
    Write-Host "[Clean] OK: not found $t"
  }
}
