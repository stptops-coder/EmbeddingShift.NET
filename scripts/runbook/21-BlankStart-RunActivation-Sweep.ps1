# DEPRECATED
# This script name is kept for backward compatibility.
# Use: .\scripts\runbook\21-AcceptanceSweep-Deterministic.ps1
#
# Expected outcomes:
#   - This wrapper simply forwards to 21-AcceptanceSweep-Deterministic.ps1.
#   - Keeping the old filename avoids breaking notes, bookmarks, and older runbooks.
#
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$target = Join-Path $PSScriptRoot '21-AcceptanceSweep-Deterministic.ps1'
if (-not (Test-Path -LiteralPath $target)) {
    throw "Target runbook script not found: $target"
}

& $target @Args
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
