<# 
Lab helper: initialize a clean, isolated run environment (tenant + data root).

Goals:
- Keep the "blank field" workflow reproducible (wipe lab-specific folders).
- Avoid hidden influence from leftover EMBEDDINGSHIFT_* environment variables.

Notes:
- This script only touches the lab data root and the lab tenant results folder.
- It does NOT modify your git repo. (You decide if/when to commit.)
#>

[CmdletBinding()]
param(
  [string]$Tenant = ("lab-{0}" -f (Get-Date -Format "yyyyMMdd-HHmmss")),
  [string]$DatasetRoot = "",
  [string]$DataRoot = "",
  [switch]$WipeLab
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot {
  param([string]$StartPath)
  $p = (Resolve-Path -LiteralPath $StartPath).Path
  while ($true) {
    if (Test-Path -LiteralPath (Join-Path $p 'src')) {
      $csproj = Join-Path $p 'src/EmbeddingShift.ConsoleEval/EmbeddingShift.ConsoleEval.csproj'
      if (Test-Path -LiteralPath $csproj) { return $p }
    }
    $parent = Split-Path -Parent $p
    if ($parent -eq $p) { throw "RepoRoot not found starting at '$StartPath'." }
    $p = $parent
  }
}

function Write-KV([string]$k, [string]$v) {
  Write-Host ("{0} = {1}" -f $k, $v)
}

$repoRoot = Resolve-RepoRoot -StartPath $PSScriptRoot
Set-Location -LiteralPath $repoRoot

# Always clean EMBEDDINGSHIFT_* for this PowerShell process to avoid "mixing".
Get-ChildItem Env:EMBEDDINGSHIFT_* -ErrorAction SilentlyContinue | Remove-Item -ErrorAction SilentlyContinue

if ([string]::IsNullOrWhiteSpace($DatasetRoot)) {
  $DatasetRoot = Join-Path $repoRoot 'samples/insurance'
}
if (-not (Test-Path -LiteralPath $DatasetRoot)) {
  throw "DatasetRoot not found: $DatasetRoot"
}

if ([string]::IsNullOrWhiteSpace($DataRoot)) {
  $DataRoot = Join-Path $repoRoot ("data/_lab/{0}" -f $Tenant)
}

# Apply env vars (dotnet child processes inherit them).
$env:EMBEDDINGSHIFT_TENANT = $Tenant
$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $DatasetRoot
$env:EMBEDDINGSHIFT_DATA_ROOT = $DataRoot

$resultsTenant = Join-Path $repoRoot ("results/insurance/tenants/{0}" -f $Tenant)

if ($WipeLab) {
  Write-Host "[Lab-Init] Wiping lab folders..."
  Remove-Item -Recurse -Force -LiteralPath $DataRoot -ErrorAction SilentlyContinue
  Remove-Item -Recurse -Force -LiteralPath $resultsTenant -ErrorAction SilentlyContinue
}

Write-Host "[Lab-Init] Environment ready."
Write-KV 'RepoRoot' $repoRoot
Write-KV 'Tenant' $Tenant
Write-KV 'DatasetRoot' $DatasetRoot
Write-KV 'DataRoot' $DataRoot
Write-KV 'ResultsTenant' $resultsTenant

Write-Host ""
Write-Host "Next:"
Write-Host (".\scripts\lab\02-Lab-Ingest.ps1 -Dataset MiniInsurance")
