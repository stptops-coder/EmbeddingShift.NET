# EmbeddingShift ConsoleEval CLI — Onboarding (Quickstart)

Fast onboarding for `EmbeddingShift.ConsoleEval`.
Goal: Train → Run → Compare → Promote/Rollback.

## Invoke

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- <command> <args>
```

## Tenant (global)

Either:

```powershell
--tenant insurer-a
```

or:

```powershell
$env:EMBEDDINGSHIFT_TENANT = "insurer-a"
```

Tenant scopes run artifacts under:
`results\<domainKey>\tenants\<tenant>\runs\...`

## Simulation knobs (current repo)

In this repo version, `--backend=openai` is **not implemented** (throws `NotSupportedException`).
Use `sim`.

Typical globals:

```powershell
--backend=sim
--sim-mode=deterministic   # or: noisy
--sim-noise=0.01           # meaningful if noisy
--sim-algo=semantic-hash   # or: sha256
--sim-char-ngrams=1
```

Optional semantic cache:

```powershell
--semantic-cache
--cache-max=2000
--cache-hamming=6
--cache-approx=1
```

## One-command sanity check

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a smoke-all
```

`smoke-all` orchestrates:
- `run-smoke-demo`
- `domain mini-insurance run`
- `domain mini-insurance posneg-train --mode=micro`
- `domain mini-insurance posneg-run --use-latest`

## Production-style loop (Mini-Insurance PosNeg)

### (Optional) Generate a staged dataset

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance dataset-generate --name=myds
```

If you want to force usage of that generated dataset:

```powershell
$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = "...\results\insurance\datasets\myds\staged"
```

### Train (PosNeg)

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a `
  domain mini-insurance posneg-train --mode=prod --hardneg-topk=20 --cancel-eps=0.001
```

### Run (writes Baseline + PosNeg run artifacts)

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a `
  domain mini-insurance posneg-run --use-latest --scale=1.0
```

### Compare → Decide → Promote

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a `
  runs-compare --workflow=MiniInsurance-PosNeg --metric=ndcg@3 --top=10
```

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a `
  runs-decide --workflow=MiniInsurance-PosNeg --metric=ndcg@3 --eps=0.001
```

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a `
  runs-promote --workflow=MiniInsurance-PosNeg --run=<RunId>
```

Rollback if needed:

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a `
  runs-rollback --workflow=MiniInsurance-PosNeg
```

## Where to look for outputs

Run artifacts (for compare/promote):
- `results\insurance\tenants\<tenant>\runs\<WorkflowName>\<RunId>\run.json`

Training artifacts (latest/best/history):
- `shift-training-history`
- `shift-training-best`
- `shift-training-inspect`

Discovery:

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- help
dotnet run --project src/EmbeddingShift.ConsoleEval -- domain list
dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance help
```
