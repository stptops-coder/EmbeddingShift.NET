param(
    [string]$Tenant,
    [int]$Seed = 1006,

    [int]$Policies = 80,
    [int]$Queries = 160,
    [int]$Stages = 3
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Tenant)) {
    $Tenant = $env:EMBEDDINGSHIFT_TENANT
}
if ([string]::IsNullOrWhiteSpace($Tenant)) {
    $Tenant = 'insurer-a'
}

Write-Host "[FullRun] tenant=$Tenant seed=$Seed policies=$Policies queries=$Queries stages=$Stages"

& "$PSScriptRoot\..\run\Run-MiniInsurance-SemHashNgrams1.ps1" `
    -Tenant $Tenant `
    -Seed $Seed `
    -Policies $Policies `
    -Queries $Queries `
    -Stages $Stages
