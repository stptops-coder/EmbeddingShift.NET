#requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Thin wrapper: keep the canonical entrypoint stable.
& (Join-Path $PSScriptRoot "..\runbook-internal\90-Tests-Samples.ps1")
