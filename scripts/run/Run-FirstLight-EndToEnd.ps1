[CmdletBinding()]
param(
  # Optional override. If not provided, the script auto-detects the repo root.
  [string]$RepoRoot,

  # Domain key used for internal results paths inside a RunRoot (default: insurance)
  [string]$ResultsDomain = "insurance",

  # ConsoleEval domain id (argument to the CLI).
  [string]$DomainId = "mini-insurance",

  # Optional behavior
  [switch]$Build,
  [bool]$WriteJsonIndex = $true,
  [switch]$OpenSummary,
  [switch]$OpenHealth
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Import helpers
. (Join-Path $PSScriptRoot "..\lib\RepoRoot.ps1")
. (Join-Path $PSScriptRoot "..\lib\DotNet.ps1")

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
  # Convention: scripts\run\... lives under <RepoRoot>\scripts\...
  # So the repo root is two levels up from this script folder.
  $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
}
$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$runScript    = Join-Path $RepoRoot "scripts\run\Run-FirstLight-MultiStage.ps1"
$healthScript = Join-Path $RepoRoot "scripts\inspect\Inspect-RunRootHealth.ps1"
$inspectScript = Join-Path $RepoRoot "scripts\inspect\Inspect-RunRoot.ps1"

Assert-Dir  $RepoRoot "RepoRoot"
Assert-File $runScript "Run script"
Assert-File $healthScript "Health script"
Assert-File $inspectScript "Inspect script"

function Get-RunRoots([string]$repoRoot, [string]$resultsDomain) {
  $base = Join-Path $repoRoot "results\_scratch\EmbeddingShift.FirstLight"
  if (-not (Test-Path -LiteralPath $base)) { return @() }
  return @(Get-ChildItem -LiteralPath $base -Directory -ErrorAction SilentlyContinue)
}

$before = Get-RunRoots $RepoRoot $ResultsDomain
$beforeSet = @{}
foreach ($d in $before) { $beforeSet[$d.FullName] = $true }

Push-Location -LiteralPath $RepoRoot
try {
  if ($Build) {
    Invoke-DotNet -Args @("build") | Out-Host
  }

  Write-Host ("[Run] " + $runScript)
  # Capture the last output line if the script prints the runRoot.
  $runOutput = & $runScript -RepoRoot $RepoRoot -ResultsDomain $ResultsDomain -DomainId $DomainId
  $lastLine = $null
  if ($runOutput) {
    $lastLine = @($runOutput | Select-Object -Last 1)[0]
  }

  $after = Get-RunRoots $RepoRoot $ResultsDomain | Sort-Object LastWriteTime -Descending

  # Determine the new runroot
  $runRoot = $null
  if ($lastLine -and (Test-Path -LiteralPath $lastLine -PathType Container)) {
    $runRoot = (Resolve-Path -LiteralPath $lastLine).Path
  } else {
    foreach ($d in $after) {
      if (-not $beforeSet.ContainsKey($d.FullName)) { $runRoot = $d.FullName; break }
    }
  }

  if (-not $runRoot) {
    throw "Could not determine the newly created RunRoot."
  }

  Write-Host ""
  Write-Host ("[RunRoot] " + $runRoot)

  # 1) Summary (ConsoleEval)
  Write-Host "[Summary] runroot-summarize"
  Invoke-DotNet -Args @(
    "run",
    "--project", "src/EmbeddingShift.ConsoleEval",
    "--",
    "domain", $DomainId, "runroot-summarize",
    ("--runroot=" + $runRoot)
  ) | Out-Host

  # 2) Health
  Write-Host "[Health] Inspect-RunRootHealth -WriteReport"
  & $healthScript -RunRoot $runRoot -Domain $ResultsDomain -WriteReport
  if ($LASTEXITCODE -ne 0) { throw "Health script failed (exit=$LASTEXITCODE)" }

  # 3) Inspect (optional JSON index)
  if ($WriteJsonIndex) {
    Write-Host "[Inspect] Inspect-RunRoot -WriteJsonIndex"
    & $inspectScript -RunRoot $runRoot -Domain $ResultsDomain -WriteJsonIndex
    if ($LASTEXITCODE -ne 0) { throw "Inspect script failed (exit=$LASTEXITCODE)" }
  }

  $summaryPath = Join-Path $runRoot ("results\" + $ResultsDomain + "\reports\summary.txt")
  $healthPath  = Join-Path $runRoot ("results\" + $ResultsDomain + "\reports\health.txt")

  Write-Host ""
  Write-Host "[Artifacts]"
  Write-Host ("  Summary: " + $summaryPath)
  Write-Host ("  Health : " + $healthPath)
  
  Write-Host ""
  
  Write-Host "Next commands (copy/paste):"
  Write-Host ("  `$rr = `"{0}`"" -f $runRoot)
  Write-Host "  [Environment]::SetEnvironmentVariable('EMBEDDINGSHIFT_ROOT', `$rr, 'Process')"
  Write-Host "  dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance posneg-best --include-cancelled"
  Write-Host ("  dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance runroot-summarize --runroot=`"{0}`"" -f $runRoot)
  Write-Host "  .\scripts\inspect\Inspect-RunRoot.ps1 -RunRoot `$rr -WriteJsonIndex"

  if ($OpenSummary -and (Test-Path -LiteralPath $summaryPath)) {
    Start-Process notepad.exe -ArgumentList @($summaryPath) | Out-Null
  }
  if ($OpenHealth -and (Test-Path -LiteralPath $healthPath)) {
    Start-Process notepad.exe -ArgumentList @($healthPath) | Out-Null
  }
}
finally {
  Pop-Location
}