Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

& "$PSScriptRoot\00-Prep.ps1"
& "$PSScriptRoot\10-Build.ps1"
& "$PSScriptRoot\20-FullRun-MiniInsurance.ps1"
& "$PSScriptRoot\30-PosNegRun-Scale10.ps1"
& "$PSScriptRoot\40-Segment-Oracle.ps1"
& "$PSScriptRoot\41-Segment-GapTau0.ps1"
& "$PSScriptRoot\90-Tests-Samples.ps1"
