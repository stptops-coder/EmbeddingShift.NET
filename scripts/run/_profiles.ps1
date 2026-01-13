Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-EmbeddingShiftProfileFlags {
  param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('v1','v2','v3','v4')]
    [string] $Profile
  )

  switch ($Profile) {

    # v1: deterministic, sha256, NO semantic cache
    'v1' {
      return @(
        '--backend=sim',
        '--provider=sim',
        '--sim-mode=deterministic',
        '--sim-algo=sha256',
        '--sim-noise=0',
        '--no-semantic-cache'
      )
    }

    # v2: deterministic, sha256, WITH semantic cache
    'v2' {
      return @(
        '--backend=sim',
        '--provider=sim',
        '--sim-mode=deterministic',
        '--sim-algo=sha256',
        '--sim-noise=0',
        '--semantic-cache'
      )
    }

    # v3: noisy, sha256, WITH semantic cache
    'v3' {
      return @(
        '--backend=sim',
        '--provider=sim',
        '--sim-mode=noisy',
        '--sim-algo=sha256',
        '--sim-noise=0.001',
        '--semantic-cache'
      )
    }

    # v4: noisy, semantic-hash, WITH semantic cache
    'v4' {
      return @(
        '--backend=sim',
        '--provider=sim',
        '--sim-mode=noisy',
        '--sim-algo=semantic-hash',
        '--sim-noise=0.001',
        '--semantic-cache'
      )
    }

    default {
      throw "Unknown profile: $Profile"
    }
  }
}
