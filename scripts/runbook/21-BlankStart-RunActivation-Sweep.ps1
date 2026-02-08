# DEPRECATED
# This script name is kept for backward compatibility.
# Use: .\scripts\runbook\21-AcceptanceSweep-Deterministic.ps1

#
# Expected outcomes:
#   - This wrapper simply forwards to 21-AcceptanceSweep-Deterministic.ps1.
#   - Keeping the old filename avoids breaking notes, bookmarks, and older runbooks.
#
& (Join-Path $PSScriptRoot '21-AcceptanceSweep-Deterministic.ps1')
