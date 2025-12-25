# PowerShell helper functions for invoking dotnet in a consistent, fail-fast way.
# Intended to be dot-sourced by scripts under .\scripts\*

function Invoke-DotNet {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory=$true)][string[]]$Args,
    [string]$WorkingDirectory
  )

  $dotnetExe = (Get-Command dotnet -ErrorAction Stop).Source
  $wdPushed = $false

  try {
    if (-not [string]::IsNullOrWhiteSpace($WorkingDirectory)) {
      Push-Location -LiteralPath $WorkingDirectory
      $wdPushed = $true
    }

    Write-Host ("[dotnet] {0} {1}" -f $dotnetExe, ($Args -join ' '))

    $lines = & $dotnetExe @Args 2>&1
    $text = ($lines | Out-String)

    if ($LASTEXITCODE -ne 0) {
      $text | Out-Host
      throw ("dotnet failed with exit code {0}" -f $LASTEXITCODE)
    }

    return $text
  }
  finally {
    if ($wdPushed) { Pop-Location }
  }
}
