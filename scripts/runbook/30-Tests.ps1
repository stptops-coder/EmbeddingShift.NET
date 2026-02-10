Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Convenience alias for the existing test runbook (samples/insurance).
& "$PSScriptRoot\90-Tests-Samples.ps1"
