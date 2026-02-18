param(
    [Parameter(Mandatory = $true)]
    [string] $RunRoot,

    [Parameter(Mandatory = $true)]
    [string] $Domain,

    [Parameter(Mandatory = $true)]
    [string] $Tenant
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$runRoot = (Resolve-Path $RunRoot).Path

$domainRoot = Join-Path $runRoot "results\$Domain"
$contractRoot = Join-Path $domainRoot "tenants\$Tenant"

# Optional top-level artifacts (reserved for future tooling).
$manifestOk = Test-Path (Join-Path $runRoot "manifest.json")
$casesOk = Test-Path (Join-Path $runRoot "cases.json")
$indexOk = Test-Path (Join-Path $runRoot "index.json")

$dataEmbeddingsOk = Test-Path (Join-Path $runRoot "data\embeddings")
$dataVectorstoreOk = Test-Path (Join-Path $runRoot "data\vectorstore")

$datasetsOk = Test-Path (Join-Path $contractRoot "datasets")
$trainingOk = Test-Path (Join-Path $contractRoot "training")
$runsOk = Test-Path (Join-Path $contractRoot "runs")
$reportsOk = Test-Path (Join-Path $contractRoot "reports")
$experimentsOk = Test-Path (Join-Path $contractRoot "experiments")
$aggregatesOk = Test-Path (Join-Path $contractRoot "aggregates")
$inspectOk = Test-Path (Join-Path $contractRoot "inspect")

Write-Host "=== RunRoot Health ==="
Write-Host "TimeUtc       : $([DateTime]::UtcNow.ToString('o'))"
Write-Host "RunRoot       : $runRoot"
Write-Host "Domain        : $Domain"
Write-Host "DomainRoot    : $domainRoot"
Write-Host "Tenant        : $Tenant"
Write-Host "ContractRoot  : $contractRoot"
Write-Host ""

Write-Host "Top-level files (optional; reserved for future tooling)"
Write-Host "  manifest.json (optional) : $manifestOk"
Write-Host "  cases.json    (optional) : $casesOk"
Write-Host "  index.json    (optional) : $indexOk"
Write-Host ""

Write-Host "Data folders"
Write-Host "  data\\embeddings  : $dataEmbeddingsOk"
Write-Host "  data\\vectorstore : $dataVectorstoreOk"
Write-Host ""

Write-Host "Contract folders (under ContractRoot)"
Write-Host "  datasets    : $datasetsOk"
Write-Host "  training    : $trainingOk"
Write-Host "  runs        : $runsOk"
Write-Host "  reports     : $reportsOk"
Write-Host "  experiments : $experimentsOk"
Write-Host "  aggregates  : $aggregatesOk"
Write-Host "  inspect     : $inspectOk"
Write-Host ""

# Persist report
$reportDir = Join-Path $contractRoot "reports"
if (-not (Test-Path $reportDir)) {
    New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
}

$reportPath = Join-Path $reportDir "health.txt"
$lines = @(
    "TimeUtc       : $([DateTime]::UtcNow.ToString('o'))",
    "RunRoot       : $runRoot",
    "Domain        : $Domain",
    "DomainRoot    : $domainRoot",
    "Tenant        : $Tenant",
    "ContractRoot  : $contractRoot",
    "",
    "Top-level files (optional; reserved for future tooling)",
    "  manifest.json (optional) : $manifestOk",
    "  cases.json    (optional) : $casesOk",
    "  index.json    (optional) : $indexOk",
    "",
    "Data folders",
    "  data\\embeddings  : $dataEmbeddingsOk",
    "  data\\vectorstore : $dataVectorstoreOk",
    "",
    "Contract folders (under ContractRoot)",
    "  datasets    : $datasetsOk",
    "  training    : $trainingOk",
    "  runs        : $runsOk",
    "  reports     : $reportsOk",
    "  experiments : $experimentsOk",
    "  aggregates  : $aggregatesOk",
    "  inspect     : $inspectOk"
)

$lines | Set-Content -Path $reportPath -Encoding UTF8
Write-Host "Wrote: $reportPath"
