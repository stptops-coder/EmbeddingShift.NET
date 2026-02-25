Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info([string]$msg) { Write-Host $msg }

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

Write-Info "[Revert] RepoRoot = $repoRoot"

# 1) Delete the custom xUnit framework file (if present)
$frameworkFile = Join-Path $repoRoot "src\EmbeddingShift.Tests\EmbeddingShiftTestFramework.cs"
if (Test-Path $frameworkFile) {
  Write-Info "[Revert] Removing $frameworkFile"
  Remove-Item -Force -LiteralPath $frameworkFile
} else {
  Write-Info "[Revert] OK: Framework file not present"
}

# 2) Remove assembly-level TestFramework attribute lines (if present)
$testProjectDir = Join-Path $repoRoot "src\EmbeddingShift.Tests"
$csFiles = Get-ChildItem -LiteralPath $testProjectDir -Recurse -File -Filter *.cs

foreach ($f in $csFiles) {
  $raw = Get-Content -LiteralPath $f.FullName -Raw
  if ($raw -match "TestFramework") {
    Write-Info "[Revert] Stripping TestFramework attribute from $($f.FullName)"
    $lines = Get-Content -LiteralPath $f.FullName
    $newLines = $lines | Where-Object { $_ -notmatch "TestFramework" }
    $tmp = $f.FullName + ".tmp"
    # Write UTF-8 explicitly
    $newLines | Set-Content -LiteralPath $tmp -Encoding utf8
    Move-Item -Force -LiteralPath $tmp -Destination $f.FullName
  }
}

# 3) Remove explicit csproj includes for the framework file (if present)
$csproj = Join-Path $repoRoot "src\EmbeddingShift.Tests\EmbeddingShift.Tests.csproj"
if (Test-Path $csproj) {
  $raw = Get-Content -LiteralPath $csproj -Raw
  if ($raw -match "EmbeddingShiftTestFramework\.cs") {
    Write-Info "[Revert] Removing csproj reference(s) to EmbeddingShiftTestFramework.cs"
    # Remove any line that references the file (keeps project formatting mostly intact)
    $lines = Get-Content -LiteralPath $csproj
    $newLines = $lines | Where-Object { $_ -notmatch "EmbeddingShiftTestFramework\.cs" }
    $tmp = $csproj + ".tmp"
    $newLines | Set-Content -LiteralPath $tmp -Encoding utf8
    Move-Item -Force -LiteralPath $tmp -Destination $csproj
  } else {
    Write-Info "[Revert] OK: No csproj reference to EmbeddingShiftTestFramework.cs"
  }
} else {
  Write-Info "[Revert] WARN: Test project csproj not found: $csproj"
}

Write-Info "[Revert] Done."
