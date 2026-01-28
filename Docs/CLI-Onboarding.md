# EmbeddingShift ConsoleEval CLI – Onboarding (Quickstart)

As of: 2026-01-27

Goal: A **reliable, code-synchronous** starter chain: Generate → Ingest → Validate → Eval → Compare/activate runs.

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

## 9) Adaptive (status)

- `adaptive` is currently a **demo** (synthetic vectors) and is **not** integrated into the ingest→eval→promote flow.
