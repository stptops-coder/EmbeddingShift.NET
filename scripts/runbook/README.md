# Runbook scripts

This folder contains the canonical PowerShell verification path for the current repo state.

Use this folder for the standard gate. Optional larger experiments are documented separately in `scripts/runbook-experimental/README.md`.

## Folder layout

- `scripts/runbook`: standard gate scripts.
- `scripts/runbook-internal`: helper scripts called by the gate scripts.
- `scripts/runbook-experimental`: optional advanced experiments, not part of the standard gate.

## Standard gate (recommended)

Run from the repository root:

```powershell
.\scripts\runbook\00-Prep.ps1
.\scripts\runbook\10-Build.ps1
.\scripts\runbook\30-Tests.ps1
.\scripts\runbook\21-AcceptanceSweep-Deterministic.ps1 -Policies 40 -Queries 80 -Stages 1 -Seed 1337 -SimAlgo semantic-hash -SimSemanticCharNGrams 1
```

Notes:
- `00-Prep.ps1` clears process-level EmbeddingShift environment variables for a clean local run.
- `10-Build.ps1` builds the solution.
- `30-Tests.ps1` is the stable test gate and calls `scripts\runbook-internal\90-Tests-Samples.ps1`.
- `21-AcceptanceSweep-Deterministic.ps1` writes isolated artifacts under `results\_scratch\EmbeddingShift.Sweep\...` by default.

## Deterministic acceptance sweep

The acceptance sweep can also be run directly:

```powershell
.\scripts\runbook\21-AcceptanceSweep-Deterministic.ps1
```

Useful options:
- `-Promote` writes activation decisions for the selected run.
- `-CompareRepoPosNeg` writes an additional reporting compare for repo PosNeg runs.
- `-IncludeRepoPosNeg` allows `runs-decide` / `runs-promote` to consider repo PosNeg candidates.

## One-command standard gate

```powershell
.\scripts\runbook\99-RunAll.ps1
```

This runs prep, build, tests, and the deterministic acceptance sweep. It intentionally stays small; advanced experiments should be launched explicitly from `scripts/runbook-experimental`.

## Inspection helpers

Useful but not required for the standard gate:

```powershell
.\scripts\runbook\05-PathAudit.ps1
.\scripts\runbook\60-Inspect-ScratchLayout.ps1
```
