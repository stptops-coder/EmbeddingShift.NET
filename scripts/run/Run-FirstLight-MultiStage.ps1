param(
  [Parameter(Mandatory=$false)]
  [string]$RepoRoot = "",

  [Parameter(Mandatory=$false)]
  [string]$ResultsDomain = "insurance",

  [Parameter(Mandatory=$false)]
  [string]$DomainId = "mini-insurance",

  [Parameter(Mandatory=$false)]
  [int]$Seed = 1337,

  [Parameter(Mandatory=$false)]
  [string]$DatasetName = "FirstLight3-$Seed",

  [Parameter(Mandatory=$false)]
  [int]$Policies = 80,

  [Parameter(Mandatory=$false)]
  [int]$Queries = 160,

  [Parameter(Mandatory=$false)]
  [int]$Stages = 3,

  [Parameter(Mandatory=$false)]
  [string]$Tenant = "insurer-a",

  [switch]$Build
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. "$PSScriptRoot\..\lib\RepoRoot.ps1"
. "$PSScriptRoot\..\lib\DotNet.ps1"


# -------------------------------------------------------------------------------------------------
# Purpose
#   End-to-end "FirstLight" demo run (generate multi-stage dataset -> run domain pipeline -> train posneg).
#   Uses tenant layout to stay compatible with other runbook scripts and ConsoleEval defaults.
#
# Preconditions
#   - Run from repo root (recommended) or any folder inside the repo.
#   - dotnet SDK installed.
#
# Postconditions
#   - A new runroot is created under: results\_scratch\EmbeddingShift.FirstLight\FirstLight3_yyyyMMdd_HHmmss
#   - Dataset is generated under tenant layout: results\<domain>\tenants\<tenant>\datasets\<dataset>\stage-*
#   - Domain pipeline + posneg training are executed using the generated dataset.
# -------------------------------------------------------------------------------------------------

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Get-RepoRoot -StartPath $PSScriptRoot
} else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}
$SlnPath  = Join-Path $RepoRoot "EmbeddingShift.sln"

if ($Build) {
  Write-Host ""
  Write-Host "dotnet build (explicit sln)"
  Invoke-DotNet @("build", $SlnPath)
  Write-Host ""
}

$RunId = (Get-Date).ToString("yyyyMMdd_HHmmss")
$RunRoot = Join-Path $RepoRoot "results\_scratch\EmbeddingShift.FirstLight\FirstLight3_$RunId"

# In this run we want a tenant-scoped layout to be compatible with the rest of the runbook.
$PrevTenant = $env:EMBEDDINGSHIFT_TENANT
$env:EMBEDDINGSHIFT_TENANT = $Tenant

try {
  Write-Host ""
  Write-Host "=== Seed $Seed / dataset $DatasetName / tenant $Tenant / domain $DomainId ==="

  # Scope all output to this run root (ConsoleEval honors EMBEDDINGSHIFT_ROOT).
  $PrevRoot = $env:EMBEDDINGSHIFT_ROOT
  $env:EMBEDDINGSHIFT_ROOT = $RunRoot

  try {
    # 1) Generate multi-stage dataset (tenant layout)
    Invoke-DotNet @(
      "run","--project","src/EmbeddingShift.ConsoleEval","--",
      "domain",$DomainId,
      "--tenant",$Tenant,
      "dataset-generate",$DatasetName,
      "--policies=$Policies","--queries=$Queries","--stages=$Stages","--seed=$Seed"
    )

    $DatasetStage0 = Join-Path $RunRoot "results\$ResultsDomain\tenants\$Tenant\datasets\$DatasetName\stage-00"

    Write-Host ""
    Write-Host "Next (PowerShell):"
    Write-Host "  `$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = `\"$DatasetStage0`\""
    Write-Host "  dotnet run --project src/EmbeddingShift.ConsoleEval -- domain $DomainId pipeline"
    Write-Host ""

    # 2) Run domain pipeline against stage-00
    $PrevDsRoot = $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT
    $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $DatasetStage0
    try {
      Invoke-DotNet @(
        "run","--project","src/EmbeddingShift.ConsoleEval","--",
        "domain",$DomainId,
        "--tenant",$Tenant,
        "pipeline"
      )
    } finally {
      $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $PrevDsRoot
    }

    # 3) Train PosNeg delta (production mode by default)
    Invoke-DotNet @(
      "run","--project","src/EmbeddingShift.ConsoleEval","--",
      "domain",$DomainId,
      "--tenant",$Tenant,
      "posneg-train","--mode=production","--cancel-epsilon=0.001"
    )

  } finally {
    $env:EMBEDDINGSHIFT_ROOT = $PrevRoot
  }
}
finally {
  $env:EMBEDDINGSHIFT_TENANT = $PrevTenant
}

Write-Output $RunRoot
