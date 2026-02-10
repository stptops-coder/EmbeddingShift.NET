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

function DotNet-RunConsoleEval {
    <#
      Runs EmbeddingShift.ConsoleEval via "dotnet run --project ... -- <args>".

      Usage:
        DotNet-RunConsoleEval @('domain','mini-insurance','pipeline')
        DotNet-RunConsoleEval @('--','domain','mini-insurance','pipeline')   # also supported
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Args,

        # Optional override. If omitted, repo root is inferred from this file location.
        [string]$RepoRoot = ''
    )

    if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
        $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    }

    $dotnetArgs = @('run', '--project', 'src/EmbeddingShift.ConsoleEval')

    if ($Args.Count -gt 0 -and $Args[0] -eq '--') {
        $dotnetArgs += $Args
    } else {
        $dotnetArgs += @('--') + $Args
    }

    Invoke-DotNet -Args $dotnetArgs -WorkingDirectory $RepoRoot
}

