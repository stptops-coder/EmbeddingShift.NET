Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-EmbeddingShiftProfileFlags {
  param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('v1','v2','v3','v4')]
    [string] $Profile
  )

  switch ($Profile) {

    # v1: deterministic, sha256, NO cache
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

    # v2: deterministic, sha256, WITH cache
    'v2' {
      return @(
        '--backend=sim',
        '--provider=sim',
        '--sim-mode=deterministic',
        '--sim-algo=sha256',
        '--sim-noise=0',
        '--semantic-cache',
        '--cache-max=5000',
        '--cache-hamming=2',
        '--cache-approx=0'
      )
    }

    # v3: noisy 0.01, sha256, NO cache
    'v3' {
      return @(
        '--backend=sim',
        '--provider=sim',
        '--sim-mode=noisy',
        '--sim-noise=0.01',
        '--sim-algo=sha256',
        '--no-semantic-cache'
      )
    }

    # v4: noisy 0.01, semantic-hash, NO cache
    'v4' {
      return @(
        '--backend=sim',
        '--provider=sim',
        '--sim-mode=noisy',
        '--sim-noise=0.01',
        '--sim-algo=semantic-hash',
        '--no-semantic-cache'
      )
    }
  }
}
