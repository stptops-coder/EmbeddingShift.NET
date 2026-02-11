param(
    [Parameter(Mandatory = $false)]
    [string] $Tenant,

    # Forward any additional (optional) args to the underlying sweep script.
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $InputArgs = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve tenant:
# - explicit -Tenant parameter wins
# - otherwise from env var
# - otherwise default (kept consistent with other runbooks)
$resolvedTenant = $Tenant
if ([string]::IsNullOrWhiteSpace($resolvedTenant)) {
    $resolvedTenant = $env:EMBEDDINGSHIFT_TENANT
}
if ([string]::IsNullOrWhiteSpace($resolvedTenant)) {
    $resolvedTenant = 'insurer-a'
}

# Ensure downstream scripts see it.
$env:EMBEDDINGSHIFT_TENANT = $resolvedTenant

$target = Join-Path $PSScriptRoot '21-AcceptanceSweep-Deterministic.ps1'
if (-not (Test-Path $target)) {
    throw "Missing script: $target"
}

# Keep it simple: RootMode is fixed to 'repo' for this runbook.
& $target -RootMode 'repo' -Tenant $resolvedTenant @InputArgs
