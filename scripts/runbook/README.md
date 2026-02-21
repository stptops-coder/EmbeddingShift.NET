# Runbook scripts

This folder contains small, reproducible PowerShell scripts for common workflows.

## Mini-Insurance (single end-to-end run)

From the repo root:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force

.\scripts\runbook\00-Prep.ps1
.\scripts\runbook\10-Build.ps1
.\scripts\runbook\20-FullRun-MiniInsurance.ps1
.\scripts\runbook\30-Tests.ps1
.\scripts\runbook\40-Health.ps1
```

Notes:
- The run is isolated under `results\_scratch\EmbeddingShift.MiniInsurance\yyyyMMdd_HHmmss` via `EMBEDDINGSHIFT_ROOT` (process scope).
- `20-FullRun-MiniInsurance.ps1` generates a dataset, runs the pipeline, creates a compare report, and makes a promotion decision.
- `40-Health.ps1` prints the most recent compare/decision report locations for the current run root.
- `30-Tests.ps1` runs the full unit/acceptance test suite (expected: all green).

## Deterministic acceptance sweep

Use:

```powershell
.\scripts\runbook\21-AcceptanceSweep-Deterministic.ps1
```

Optional:
- Add `-IncludeRepoPosNeg` to allow `runs-decide` / `runs-promote` to consider repo candidates under `runs\_repo\MiniInsurance-PosNeg` (default: off).

Legacy wrapper (kept for older notes):

```powershell
.\scripts\runbook\21-BlankStart-RunActivation-Sweep.ps1
```



This generates multiple datasets/runs into a separate scratch root under `results\_scratch\EmbeddingShift.Sweep\...`.

## Standard gate (recommended)

For a stable "greenfield" verification:

```powershell
.\scripts\runbook\00-Prep.ps1
.\scripts\runbook\10-Build.ps1
.\scripts\runbook\30-Tests.ps1
.\scripts\runbook\21-AcceptanceSweep-Deterministic.ps1 -Policies 40 -Queries 80 -Stages 1 -Seed 1337 -Promote -SimAlgo semantic-hash -SimSemanticCharNGrams 1
```

Notes:
- `30-Tests.ps1` is a convenience alias for `90-Tests-Samples.ps1`.
- `-CompareRepoPosNeg` writes a separate compare report for `runs\_repo\MiniInsurance-PosNeg` (reporting only).
- `-IncludeRepoPosNeg` allows `runs-decide` / `runs-promote` to consider repo PosNeg runs as candidates (selection).

## Other scripts (optional)

These scripts are useful, but not part of the standard gate:
- `01-CleanScratch-PreserveActive.ps1`: remove scratch while preserving shared active.
- `05-PathAudit.ps1`: diagnose path/layout issues across results roots.
- `60-Inspect-ScratchLayout.ps1`: quick inspection of the current scratch layout.
- `22-AcceptanceSweep-Deterministic-Regression.ps1` / `98-RunbookRegression.ps1`: regression wrappers for older notes.
- `25-PosNeg-Deterministic-Full.ps1` / `30-PosNegRun-Scale10.ps1`: larger PosNeg training/runs.
- `40-Segment-Oracle.ps1` / `41-Segment-GapTau0.ps1`: Segmenter experiments.
- `99-RunAll.ps1`: wrapper that runs the standard gate, plus optional extras via flags.

