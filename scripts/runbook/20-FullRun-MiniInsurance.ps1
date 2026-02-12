[CmdletBinding()]
param(
  [string]$Tenant = $(if (-not [string]::IsNullOrWhiteSpace($env:EMBEDDINGSHIFT_TENANT)) { $env:EMBEDDINGSHIFT_TENANT } else { 'insurer-a' }),
  [string]$Root = $env:EMBEDDINGSHIFT_ROOT,
  [int]$Seed = 1006,
  [int]$Policies = 80,
  [int]$Queries = 160,
  [int]$Stages = 3
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "[FullRun] tenant=$Tenant seed=$Seed policies=$Policies queries=$Queries stages=$Stages"
if (-not [string]::IsNullOrWhiteSpace($Root)) {
  Write-Host "[FullRun] root=$Root"
}

# Use hashtable splatting to avoid any positional/array binding quirks.
$invokeArgs = @{
  Tenant   = $Tenant
  Seed     = $Seed
  Policies = $Policies
  Queries  = $Queries
  Stages   = $Stages
}

if (-not [string]::IsNullOrWhiteSpace($Root)) {
  $invokeArgs['Root'] = $Root
}

& "$PSScriptRoot\..\run\Run-MiniInsurance-SemHashNgrams1.ps1" @invokeArgs
