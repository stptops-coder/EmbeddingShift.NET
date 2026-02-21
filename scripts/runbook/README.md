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
