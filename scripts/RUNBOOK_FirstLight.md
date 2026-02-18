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
  Full end-to-end run (multi-stage pipeline + summary + index + health). Returns the RunRoot via pipeline output.

- `scripts\run\Run-FirstLight-MultiStage.ps1`
  Configurable runner; useful for quicker smoke runs. Returns the RunRoot via pipeline output.

### Inspect scripts
- `scripts\inspect\Inspect-RunRootHealth.ps1`
  Validates the RunRoot folder contract (expected folders/files) and writes `reports\health.txt`.

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
$rr = .\scripts\run\Run-FirstLight-EndToEnd.ps1 -Stages 3 -Seed 1006 -SimMode deterministic -Tenant insurer-a
$rr
```

Expected outputs:
- A new RunRoot directory under:
  - `results\_scratch\EmbeddingShift.FirstLight\<RunId>\`
- Reports under the RunRoot, typically including:
  - `results\insurance\tenants\<Tenant>\reports\summary.txt`
  - `results\insurance\tenants\<Tenant>\reports\health.txt`
  - `index.json` in the RunRoot root

To see script parameters:

```powershell
Get-Help .\scripts\run\Run-FirstLight-EndToEnd.ps1 -Detailed
```

---

## 5) Quick run (MultiStage)

Use MultiStage directly for faster iterations:

```powershell
$rr = .\scripts\run\Run-FirstLight-MultiStage.ps1 -Stages 2 -Seed 1337 -SimMode deterministic -Tenant insurer-a
$rr
```

To see script parameters:

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
.\scripts\inspect\Inspect-RunRoot.ps1 -RunRoot $rr -Domain insurance -Tenant insurer-a -WriteJsonIndex
.\scripts\inspect\Inspect-RunRootHealth.ps1 -RunRoot $rr -Domain insurance -Tenant insurer-a
```

Notes:
- `manifest.json` and `cases.json` are currently **optional** placeholders (reserved for future tooling).
- `index.json` is optional as well, but recommended because it makes navigation and inspection faster.

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
