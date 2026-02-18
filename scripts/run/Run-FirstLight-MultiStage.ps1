param(
  [Parameter(Mandatory=$false)]
  [string]$RepoRoot = "",

  [Parameter(Mandatory=$false)]
  [string]$ResultsDomain = "insurance",

  [Parameter(Mandatory=$false)]
  [string]$DomainId = "mini-insurance",

  [Parameter(Mandatory=$false)]
  [string]$Tenant = "insurer-a",

  [Parameter(Mandatory=$false)]
  [int]$Policies = 80,

  [Parameter(Mandatory=$false)]
  [int]$Queries = 160,

  [Parameter(Mandatory=$false)]
  [int]$Stages = 3,

  [Parameter(Mandatory=$false)]
  [int]$Seed = 1337,

  [Parameter(Mandatory=$false)]
  [string]$DatasetName = "",

  [Parameter(Mandatory=$false)]
  [ValidateSet("deterministic","stochastic")]
  [string]$SimMode = "deterministic",

  [Parameter(Mandatory=$false)]
  [switch]$Overwrite,

  [Parameter(Mandatory=$false)]
  [switch]$Build
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Find-RepoRoot([string]$StartPath) {
  $p = Resolve-Path -LiteralPath $StartPath
  while ($true) {
    $candidate = Join-Path $p "EmbeddingShift.sln"
    if (Test-Path -LiteralPath $candidate) { return $p }
    $parent = Split-Path -Parent $p
    if ([string]::IsNullOrWhiteSpace($parent) -or ($parent -eq $p)) {
      throw "Could not find EmbeddingShift.sln starting from: $StartPath"
    }
    $p = $parent
  }
}

. "$PSScriptRoot\..\lib\DotNet.ps1"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
  $RepoRoot = Find-RepoRoot -StartPath $PSScriptRoot
} else {
  $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

if ([string]::IsNullOrWhiteSpace($DatasetName)) {
  $DatasetName = ("FirstLight{0}-{1}" -f $Stages, $Seed)
}

$SlnPath = Join-Path $RepoRoot "EmbeddingShift.sln"

if ($Build) {
  Write-Host ""
  Write-Host "dotnet build (explicit sln)"
  Invoke-DotNet @("build", $SlnPath) | Out-Host
  Write-Host ""
}

$RunId = (Get-Date).ToString("yyyyMMdd_HHmmss")
$RunRoot = Join-Path $RepoRoot ("results\_scratch\EmbeddingShift.FirstLight\FirstLight{0}_{1}" -f $Stages, $RunId)

if (Test-Path -LiteralPath $RunRoot) {
  if ($Overwrite) {
    Write-Host ("[Run] Overwrite enabled -> deleting existing run root: {0}" -f $RunRoot)
    Remove-Item -Recurse -Force -LiteralPath $RunRoot
  } else {
    throw "Run root already exists: $RunRoot. Use -Overwrite to replace it."
  }
}
New-Item -ItemType Directory -Force -Path $RunRoot | Out-Null

$PrevTenant = $env:EMBEDDINGSHIFT_TENANT
$PrevSimMode = $env:EMBEDDINGSHIFT_SIM_MODE
$PrevRoot = $env:EMBEDDINGSHIFT_ROOT

$env:EMBEDDINGSHIFT_TENANT = $Tenant
$env:EMBEDDINGSHIFT_SIM_MODE = $SimMode

try {
  Write-Host ""
  Write-Host "=== Seed $Seed / dataset $DatasetName / tenant $Tenant / domain $DomainId / sim $SimMode ==="

  # Scope all output to this run root (ConsoleEval honors EMBEDDINGSHIFT_ROOT).
  $env:EMBEDDINGSHIFT_ROOT = $RunRoot

  # 1) Generate multi-stage dataset (tenant layout)
  $genArgs = @(
    "run","--project","src/EmbeddingShift.ConsoleEval","--",
    "domain",$DomainId,
    "--tenant",$Tenant,
    "dataset-generate",$DatasetName,
    "--policies=$Policies","--queries=$Queries","--stages=$Stages","--seed=$Seed"
  )

  if ($Overwrite) { $genArgs += "--overwrite" }

  Invoke-DotNet -Args $genArgs | Out-Host

  $DatasetStage0 = Join-Path $RunRoot "results\$ResultsDomain\tenants\$Tenant\datasets\$DatasetName\stage-00"

  Write-Host ""
  Write-Host "Next (PowerShell):"
  Write-Host ("  `$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = '{0}'" -f $DatasetStage0)
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
    ) | Out-Host
  } finally {
    $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = $PrevDsRoot
  }
}
finally {
  $env:EMBEDDINGSHIFT_ROOT = $PrevRoot
  $env:EMBEDDINGSHIFT_SIM_MODE = $PrevSimMode
  $env:EMBEDDINGSHIFT_TENANT = $PrevTenant
}

# Important: emit the runroot path as the only pipeline output
Write-Output $RunRoot
