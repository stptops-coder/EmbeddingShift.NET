# EmbeddingShift ConsoleEval CLI – Onboarding (Quickstart)

As of: 2026-02-03

Goal: A **reliable, code-synchronous** starter chain: Generate → Ingest → Validate → Eval → Compare/activate runs.

## 0) Blank Start – Run Activation Sweep (recommended)

If you want a single “known good” end-to-end sequence (generate → pipeline → compare → decide → promote → rollback → optional rerun), use the runbook:

- `scripts/runbook/21-BlankStart-RunActivation-Sweep.ps1`

Example:

```powershell
cd <repo-root>

# If PowerShell blocks script execution, run from a bypass shell:
#   PowerShell -ExecutionPolicy Bypass -NoProfile

.\scripts\runbook\21-BlankStart-RunActivation-Sweep.ps1 `
  -Tenant "insurer-b" `
  -DatasetName "SweepDS" `
  -Seed 1337 `
  -Metric "ndcg@3" `
  -Top 10 `
  -DoRerun
```

Notes:
- The runbook uses an isolated temp root (`$env:TEMP\EmbeddingShift.Sweep\...`) so it does not mutate your normal `results/` tree.
- “Rerun” is an optional verification step. It replays the *workflow command* (not a bit-for-bit copy of a previous run directory).

---

---

## 1) Standard invocation

**Prefer copy/paste multi-step sequences?**

- PowerShell runbook: `scripts/RUNBOOK_FirstLight.md`
- ConsoleEval how-to: `src/EmbeddingShift.ConsoleEval/HowToRun.md`


```powershell
cd <repo-root>
dotnet run --project src/EmbeddingShift.ConsoleEval -- help
```

## 2) Global flags (exactly as printed by `help`)


> **Note on help:** Per-command `--help` is not implemented; use the top-level `help` (or `domain <id> help`).

- `--tenant=<key>  |  --tenant <key>     (optional) writes Mini-Insurance under results/insurance/tenants/<key>/...`
- `--provider=sim|openai-echo|openai-dryrun`
- `--backend=sim|openai`
- `--method=A`
- `--sim-mode=deterministic|noisy`
- `--sim-noise=<float>`
- `--sim-algo=sha256|semantic-hash`
- `--sim-char-ngrams=0|1`
- `--semantic-cache | --no-semantic-cache`
- `--cache-max=<int>  --cache-hamming=<int>  --cache-approx=0|1`

Note: `--tenant` is parsed globally, set as ENV, and removed before dispatch. This is intentional.

---

## 3) Quick sanity: demo smoke

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a run-smoke-demo --baseline
```

---

## 4) Mini-Insurance: generate a staged dataset and set the root

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance dataset-generate MyDS --stages 3 --policies 40 --queries 80 --seed 1337 --overwrite

$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = "results/insurance/datasets/MyDS/stage-00"
```

---

## 5) Canonical flow: Ingest → Validate → Eval

### 5.1 Ingest

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-dataset `
  $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT\policies `
  $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT\queries `
  MyDataset `
  --chunk-size=1000 --chunk-overlap=100
```

### 5.2 Validate

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- dataset-validate MyDataset --role=refs --require-state --require-chunk-manifest
dotnet run --project src/EmbeddingShift.ConsoleEval -- dataset-validate MyDataset --role=queries --require-state
```

### 5.3 Eval

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- eval MyDataset --baseline
```

---

## 6) One-shot: run-smoke (your dataset)

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- run-smoke `
  $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT\policies `
  $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT\queries `
  MyDataset `
  --force-reset --baseline
```

---

## 7) PosNeg (learned delta) – Mini-Insurance domain pack

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance posneg-train --mode=micro
dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance posneg-run --latest
```

---

## 8) Compare/activate runs (Compare → Decide → Active)

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a runs-compare --metric ndcg@3 --top 10
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a runs-decide --metric ndcg@3 --eps 1e-6 --apply
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a runs-active --metric ndcg@3
```

---



### Replay a run (optional)

Each run folder contains `run.json`, `report.md` and (when captured) `run_request.json`. If `run_request.json` is present, you can replay the original command:

```
runs-rerun --run-dir=<path-to-a-run-folder>
```

Use `--print` to inspect the reconstructed command without executing it.
## 9) Adaptive (status)

- `adaptive` is currently a **demo** (synthetic vectors) and is **not** integrated into the ingest→eval→promote flow.
