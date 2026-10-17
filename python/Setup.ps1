[CmdletBinding()]
param([string]$PythonExecutable = '')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$venvPath = Join-Path $PSScriptRoot '.venv'
$relativeExe = 'bin/python'
if ($env:OS -eq 'Windows_NT') { $relativeExe = 'Scripts/python.exe' }
$pythonExe = Join-Path $venvPath $relativeExe

if (-not (Test-Path -LiteralPath $pythonExe)) {
    $names = @('py', 'python', 'python3')
    if (-not [string]::IsNullOrWhiteSpace($PythonExecutable)) { $names = @($PythonExecutable) }
    $bootstrap = Get-Command -Name $names -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $bootstrap) { throw 'Install Python 3.11 or newer first.' }
    $bootstrapOptions = @()
    if ($bootstrap.Name -match '^py(\.exe)?$') { $bootstrapOptions = @('-3') }
    & $bootstrap.Source @bootstrapOptions -m venv $venvPath
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the local Python environment.' }
}
& $pythonExe -m pip install -r (Join-Path $PSScriptRoot 'requirements.txt') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Could not install the Python dependencies.' }
Write-Host "Python cosine environment ready: $pythonExe"
