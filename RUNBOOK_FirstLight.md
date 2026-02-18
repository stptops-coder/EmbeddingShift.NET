# RUNBOOK — Portable Scripts (FirstLight)

This runbook describes how to run the **FirstLight** workflow end-to-end using the repository scripts, and how to inspect the produced **RunRoot** artifacts.

**Goal:** clone the repo → run scripts from the repo root → get a reproducible RunRoot with `reports/summary.txt` and inspection outputs.


## Runbooks in this repo

- **FirstLight (baseline / first-delta)** is documented below.
- **PosNeg (deterministic)**: `scripts/runbook/25-PosNeg-Deterministic-Full.ps1`
- **Acceptance sweep (deterministic)**: `scripts/runbook/21-AcceptanceSweep-Deterministic.ps1`

Tip: The scripts default to `results\_scratch\...` inside the repo (so you don’t depend on `%TEMP%`).

---

## 1) Prerequisites

- Windows PowerShell 5.1 **or** PowerShell 7+
- .NET SDK installed (repo targets .NET 8)

You should run all commands **from the repository root** (the folder that contains `scripts/` and `src/`).

---

## 2) One-time setup (recommended)

This avoids “not digitally signed” / blocked script problems.

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
Get-ChildItem .\scripts -Recurse -Filter *.ps1 | Unblock-File
```

Notes:
- `-Scope Process` affects only the current PowerShell session.
- `Unblock-File` removes the Windows “downloaded from internet” marker (Zone.Identifier).

---

## 3) Script map

### Run scripts
- `scripts\run\Run-FirstLight-EndToEnd.ps1`  
  Full end-to-end run (build + dataset generate + training + multi-stage runs + reports).

- `scripts\run\Run-FirstLight-MultiStage.ps1`  
  Configurable runner; useful for quicker smoke runs.

### Inspect scripts
- `scripts\inspect\Inspect-RunRootHealth.ps1`  
  Validates the RunRoot folder contract (expected folders/files).

- `scripts\inspect\Inspect-RunRoot.ps1`  
  Summarizes a RunRoot and can generate `index.json` via `-WriteJsonIndex`.

### Shared helpers
- `scripts\lib\RepoRoot.ps1`  
  Repo-root discovery helper.

- `scripts\lib\DotNet.ps1`  
  Helper for `dotnet` invocation and related functionality.

---

## 4) Standard run (End-to-End)

Run from repo root:

```powershell
.\scripts\run\Run-FirstLight-EndToEnd.ps1 -Build -OpenSummary -OpenHealth
```

Note:
- `Run-FirstLight-EndToEnd.ps1` forwards `-Tenant`, `-Seed`, `-Policies`, `-Queries`, `-Stages`, `-SimMode`, and `-Overwrite` to `Run-FirstLight-MultiStage.ps1`.
- Example:
  ```powershell
  .\scripts\run\Run-FirstLight-EndToEnd.ps1 -Stages 3 -Seed 1006
  ```


Expected outputs:
- A new RunRoot directory under:
  - `results\_scratch\EmbeddingShift.FirstLight\<RunId>\`
- Reports under the RunRoot, typically including:
  - `reports\summary.txt`
  - `reports\health.txt`

To see script parameters (if help is available):

```powershell
Get-Help .\scripts\run\Run-FirstLight-EndToEnd.ps1 -Detailed
```

---

## 5) Quick run (MultiStage)

Use MultiStage directly for faster iterations:

```powershell
.\scripts\run\Run-FirstLight-MultiStage.ps1 -Build -Policies 50 -Queries 120 -Stages 2 -Seeds @(1337)
```

To see script parameters (if help is available):

```powershell
Get-Help .\scripts\run\Run-FirstLight-MultiStage.ps1 -Detailed
```

---

## 6) Find and inspect the latest RunRoot

### 6.1 Find latest RunRoot

```powershell
$rr = (Get-ChildItem .\results\_scratch\EmbeddingShift.FirstLight -Directory |
       Sort-Object LastWriteTime -Descending |
       Select-Object -First 1).FullName
$rr
```

### 6.2 Health check + create index

```powershell
.\scripts\inspect\Inspect-RunRootHealth.ps1 -RunRoot $rr
.\scripts\inspect\Inspect-RunRoot.ps1 -RunRoot $rr -WriteJsonIndex
```

Important note:
- `index.json` is created by `Inspect-RunRoot.ps1 -WriteJsonIndex`.
  If Health reports `index.json` as missing **before** you run Inspect, that is expected.

---

## 7) RunRoot context for ConsoleEval commands

Some ConsoleEval commands read/write artifacts relative to an **active RunRoot**.
To force ConsoleEval to operate in a specific RunRoot in the current session:

```powershell
$rr = "<full path to your RunRoot>"
[Environment]::SetEnvironmentVariable("EMBEDDINGSHIFT_ROOT", $rr, "Process")
```

---

## 8) Useful ConsoleEval commands (optional)

### 8.1 Summarize a specific RunRoot (without relying on context)

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance runroot-summarize --runroot="$rr"
```

### 8.2 Inspect PosNeg training artifacts (within the active RunRoot)

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance posneg-best --include-cancelled
dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance posneg-inspect
dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance posneg-history 20 --include-cancelled
```

If these commands show “unexpected history”, it is usually because the RunRoot context differs
(or you did not set `EMBEDDINGSHIFT_ROOT` for the session).

---

## 9) Typical RunRoot contents

A RunRoot typically contains (names may evolve over time):

- `datasets/` — generated policies/queries/cases inputs
- `training/` — shift training results (PosNeg etc.)
- `runs/` — per-stage / per-seed run outputs
- `reports/` — `summary.txt`, `health.txt`, etc.
- `manifest.json` — high-level run metadata
- `cases.json` — evaluated cases and metrics

Some folders may be empty depending on the current feature set (e.g., `vectorstore/`).

---

## 10) Troubleshooting

### 10.1 “Script is not digitally signed” / cannot load
Run the one-time setup (ExecutionPolicy Process + Unblock-File):

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
Get-ChildItem .\scripts -Recurse -Filter *.ps1 | Unblock-File
```

To check whether a script is blocked (Zone.Identifier):

```powershell
Get-Item .\scripts\run\Run-FirstLight-EndToEnd.ps1 -Stream Zone.Identifier -ErrorAction SilentlyContinue
```

### 10.2 Health says `index.json` is missing
Run:

```powershell
.\scripts\inspect\Inspect-RunRoot.ps1 -RunRoot $rr -WriteJsonIndex
```

### 10.3 PosNeg cancel-out / all improvements are zero
In deterministic simulation, the embedding algorithm choice can matter a lot.
Mitigations:
- Ensure a stable deterministic sim algorithm is used (e.g., `sha256`)
- Increase dataset size (Policies/Queries)
- Use production training mode (if exposed by the scripts)


---

## Mini-Insurance runbook scripts (single run)

From the repo root:

```powershell
.\scripts\runbook\00-Prep.ps1
.\scripts\runbook\10-Build.ps1
.\scripts\runbook\20-FullRun-MiniInsurance.ps1
.\scripts\runbook\90-Tests-Samples.ps1
.\scripts\runbook\40-Health.ps1
```

Notes:
- The run is isolated under `results\_scratch\EmbeddingShift.MiniInsurance\yyyyMMdd_HHmmss` via `EMBEDDINGSHIFT_ROOT` (process scope).
- `20-FullRun-MiniInsurance.ps1` generates a dataset, runs the pipeline, creates a compare report, and makes a promotion decision.
- `90-Tests-Samples.ps1` runs the unit/acceptance test suite against the sample dataset (expected: 77/77 green).
