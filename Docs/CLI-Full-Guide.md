# EmbeddingShift ConsoleEval CLI — Full Guide

This is the comprehensive reference for the CLI implemented in `src/EmbeddingShift.ConsoleEval`.
It is structured as:

1. Execution model (dotnet + CLI)
2. Global options and environment variables
3. Workflow maps (from Generate/Ingest to Production-style promotion)
4. Command reference (purpose, syntax, key options, artifacts)

Note: In this repo version, the OpenAI backend is **not implemented** (`--backend=openai` throws `NotSupportedException`).

---

## 1) Execution model

### 1.1 Standard invocation

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- <command> <args>
```

### 1.2 Recommended for repeated runs

```powershell
dotnet build -c Release
dotnet run --no-build -c Release --project src/EmbeddingShift.ConsoleEval -- <command> <args>
```

---

## 2) Global options and environment variables

### 2.1 Tenant (most important)

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

---

### 2.2 Backend and simulation globals

#### Backend
- `--backend=sim|openai` (OpenAI not implemented in this repo; use `sim`)

#### Simulation behavior
- `--sim-mode=deterministic|noisy`
- `--sim-noise=<float>` (only meaningful if `sim-mode=noisy`)
- `--sim-algo=semantic-hash|sha256`
- `--sim-char-ngrams=1` (enables char n-gram behavior)

Example:

```powershell
--backend=sim --sim-mode=deterministic --sim-algo=semantic-hash
```

---

### 2.3 Semantic cache (optional)

- `--semantic-cache` / `--no-semantic-cache`
- `--cache-max=<int>`
- `--cache-hamming=<int>`
- `--cache-approx=<0|1>`

Example:

```powershell
--semantic-cache --cache-max=2000 --cache-hamming=6 --cache-approx=1
```

---

### 2.4 Location overrides (environment)

#### Root override
If you want results and data rooted somewhere else:

```powershell
$env:EMBEDDINGSHIFT_ROOT = "D:\work\EmbeddingShiftRunRoot"
```

#### Mini-Insurance dataset root override
If you generate a dataset via `domain mini-insurance dataset-generate` and want to force using it:

```powershell
$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = "...\results\insurance\datasets\<name>\staged"
```

---

## 3) Directory layout (what is written where)

The CLI uses two conceptual roots:

- `results\...` for human-facing run artifacts and domain outputs
- `data\...` for embedding/state/manifests used by dataset ingest and evaluation

Key locations you will inspect most:

- Run artifacts (for compare/promote):
  `results\insurance\tenants\<tenant>\runs\<WorkflowName>\<RunId>\run.json`

- Dataset ingest manifests:
  `data\manifests\<dataset>\<role>\manifest_latest.json`

- Dataset space state:
  `data\state\<dataset>\<role>\space_state.json`

---

## 4) Workflow maps

### 4.1 Track A — Dataset utilities (quick checking)
This track is ideal for verifying ingest + embeddings + evaluation quickly.
It does **not** create `run.json` artifacts used by `runs-compare/promote`.

Typical sequence:

1. `ingest-dataset`
2. `dataset-validate`
3. `eval` (optionally gated)

### 4.2 Track B — Run-based production loop
This track produces explicit run artifacts (`run.json`) and supports:
Compare → Decide → Promote/Rollback.

In this repo version, the most complete producer of run artifacts is:
- `domain mini-insurance posneg-run` (writes baseline + posneg runs)

Typical sequence:

1. (Optional) `domain mini-insurance dataset-generate`
2. `domain mini-insurance posneg-train`
3. `domain mini-insurance posneg-run`
4. `runs-compare` / `runs-best` / `runs-decide`
5. `runs-promote` or `runs-rollback`

---

## 5) Command reference

### 5.1 Discovery and help

#### `help`
Purpose: print available commands and global flags.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- help
```

#### `domain list`
Purpose: list registered domain packs.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- domain list
```

#### `domain mini-insurance help`
Purpose: show Mini-Insurance domain pack commands.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance help
```

---

### 5.2 Smoke orchestration

#### `smoke-all`
Purpose: end-to-end verification of the core paths.
It orchestrates:
- `run-smoke-demo`
- `domain mini-insurance run`
- `domain mini-insurance posneg-train --mode=micro`
- `domain mini-insurance posneg-run --use-latest`

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a smoke-all
```

---

### 5.3 Dataset ingest commands

#### `ingest-dataset` (canonical)
Purpose: ingest references + queries into the embedding store.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-dataset <dataset> --refs=<path> --queries=<path>
```

Key options:
- `--refs=<path>` (default: `samples\demo`)
- `--queries=<path>` (default: `samples\demo\queries.json`)
- `--refs-plain` (use plain refs ingest instead of chunk-first)
- `--chunk-size=<int>` / `--chunk-overlap=<int>`
- `--no-recursive`

Artifacts:
- Embeddings under `data\embeddings\...`
- Manifests under `data\manifests\...` (if chunk-first)
- Space state under `data\state\...`

---

#### `ingest-refs`
Purpose: ingest references only (plain mode).

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-refs <path> <dataset>
```

Notes:
- If no args are provided, the command uses demo defaults.

---

#### `ingest-refs-chunked`
Purpose: ingest references only (chunk-first mode).

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-refs-chunked <path> <dataset> --chunk-size=1000 --chunk-overlap=100
```

Key options:
- `--chunk-size=<int>` (default 1000)
- `--chunk-overlap=<int>` (default 100)
- `--no-recursive`

---

#### `ingest-queries`
Purpose: ingest queries only.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-queries <path> <dataset>
```

---

#### `ingest-inspect`
Purpose: inspect ingest artifacts for a dataset/role.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-inspect <dataset> --role=refs
```

Key options:
- `--role=refs|queries` (default: refs)

---

#### `ingest-legacy` (deprecated)
Purpose: legacy ingest using `r.txt` and `q.txt`.
Recommendation: use `ingest-dataset` instead.

---

### 5.4 Dataset hygiene and gates

#### `dataset-status`
Purpose: show state/manifest availability per role.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- dataset-status <dataset> --role=all
```

---

#### `dataset-validate`
Purpose: validate that required ingest artifacts exist.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- dataset-validate <dataset> --role=all --require-state --require-chunk-manifest
```

Exit codes:
- `0` ok
- `2` failed

---

#### `dataset-reset`
Purpose: reset embeddings/state (and optionally manifests) for a dataset.
Important: by default it runs in preview mode; nothing is deleted unless `--force` is provided.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- dataset-reset <dataset> --role=all
```

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- dataset-reset <dataset> --role=all --force
```

Key options:
- `--role=refs|queries|all`
- `--force`
- `--keep-manifests`

---

### 5.5 Evaluation and run (non-artifact runs)

#### `eval`
Purpose: evaluate a dataset. Optionally runs a baseline and applies an acceptance gate.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- eval <dataset> --baseline --gate-profile=rank+cosine --gate-eps=0.001
```

Key options:
- `--baseline`
- `--gate-profile=rank|rank+cosine`
- `--gate-eps=<double>` (default 0.001)

Notes:
- `--shift` exists but currently supports only `identity` and `zero` (placeholder behavior in this repo state).

---

#### `run`
Purpose: convenience orchestration: ingest-dataset + eval.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- run <dataset> --refs=<path> --queries=<path>
```

Notes:
- This does not write `run.json` artifacts used by `runs-compare/promote`.

---

#### `run-smoke` / `run-smoke-demo`
Purpose: reset → ingest → validate → eval with gate.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- run-smoke-demo
```

---

### 5.6 Mini-Insurance domain pack

#### `domain mini-insurance dataset-generate`
Purpose: create a staged dataset under `results\insurance\datasets\<name>\staged`.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance dataset-generate --name=myds
```

Common options (see `domain mini-insurance help` for exact current set):
- `--name=<string>`
- `--policy-count=<int>`
- `--query-count=<int>`

---

#### `domain mini-insurance posneg-train`
Purpose: train a global delta vector via PosNeg pairs and persist a training result (latest/best/history).

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a \
  domain mini-insurance posneg-train --mode=prod --hardneg-topk=20 --cancel-eps=0.001
```

Key options:
- `--mode=micro|prod` (default: micro)
- `--hardneg-topk=<int>` (default: 5)
- `--cancel-eps=<double>` (default: 0.001)

---

#### `domain mini-insurance posneg-run`
Purpose: run evaluation baselines + posneg-shifted and write run artifacts (`run.json`) suitable for compare/promote.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a \
  domain mini-insurance posneg-run --use-latest --scale=1.0
```

Key options:
- `--use-latest` (otherwise it uses `best`)
- `--scale=<double>` (default: 1.0)

Artifacts:
- per-run `run.json` under `results\insurance\tenants\<tenant>\runs\MiniInsurance-PosNeg\...`
- per-query JSONs (baseline vs posneg)
- markdown metrics summaries

---

#### `domain mini-insurance run` (pipeline / legacy)
Purpose: execute the Mini-Insurance pipeline (baseline/first/delta/learned-delta style flows).
This is useful for experimentation, but the production-style loop is currently centered on PosNeg.

---

### 5.7 Training result inspection (generic)

These commands work on persisted training results (latest/best/history) by workflow name.

#### `shift-training-history`

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- shift-training-history --workflow=mini-insurance-posneg --domainKey=insurance --include-cancelled
```

#### `shift-training-best`

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- shift-training-best --workflow=mini-insurance-posneg --domainKey=insurance
```

#### `shift-training-inspect`

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- shift-training-inspect --workflow=mini-insurance-posneg --domainKey=insurance --which=latest
```

---

### 5.8 Runs management (production loop)

These commands operate on run artifacts under:
`results\<domainKey>\tenants\<tenant>\runs\<workflow>\<runId>\run.json`

#### `runs-history`

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a runs-history --workflow=MiniInsurance-PosNeg
```

#### `runs-compare`

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a \
  runs-compare --workflow=MiniInsurance-PosNeg --metric=ndcg@3 --top=10 --out=compare.md
```

#### `runs-best`

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a runs-best --workflow=MiniInsurance-PosNeg --metric=ndcg@3
```

#### `runs-active`

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a runs-active --workflow=MiniInsurance-PosNeg
```

#### `runs-decide`
Compares best vs active and decides if promotion is warranted (gate via epsilon).

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a runs-decide --workflow=MiniInsurance-PosNeg --metric=ndcg@3 --eps=0.001
```

#### `runs-promote`

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a runs-promote --workflow=MiniInsurance-PosNeg --run=<RunId>
```

#### `runs-rollback`

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a runs-rollback --workflow=MiniInsurance-PosNeg
```

---

### 5.9 Batch runner

#### `runs-matrix`
Purpose: execute a matrix of variants described by a JSON spec; optionally compare, write summaries, and open output.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- runs-matrix --spec=...\matrix.json --tenant=insurer-a --open
```

Key options:
- `--spec=<file>` (required)
- `--tenant=<t>` (default: `default`)
- `--domainKey=<key>` (affects default runs-root resolution)
- `--runs-root=<path>` (override)
- `--dry` (print plan only)
- `--open` (open output folder)
- `--timeout-sec=<int>` (default: 600)

Spec model reference: `RunMatrixSpec`.

---

## 6) Troubleshooting (common cases)

### 6.1 `--backend=openai` fails
Expected in this repo state (OpenAI backend not implemented). Use `--backend=sim`.

### 6.2 Dataset validate fails
Most common causes:
- You ran `ingest-dataset` but forgot to ingest queries or refs.
- You expected chunk-first manifests but ingested plain.

Use:
- `ingest-inspect <dataset> --role=refs`
- `dataset-status <dataset> --role=all`

### 6.3 `dataset-reset` did nothing
By design: it previews unless `--force` is provided.

### 6.4 Compare/promote shows no runs
Compare/promote requires `run.json` artifacts. In this repo version, the easiest producer is:
- `domain mini-insurance posneg-run`

---

## 7) Cheat sheet

### End-to-end (production loop)

```powershell
# Train
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a \
  domain mini-insurance posneg-train --mode=prod --hardneg-topk=20 --cancel-eps=0.001

# Run (produces run.json)
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a \
  domain mini-insurance posneg-run --use-latest --scale=1.0

# Compare → Decide → Promote
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a \
  runs-compare --workflow=MiniInsurance-PosNeg --metric=ndcg@3 --top=10

dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a \
  runs-decide --workflow=MiniInsurance-PosNeg --metric=ndcg@3 --eps=0.001

dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a \
  runs-promote --workflow=MiniInsurance-PosNeg --run=<RunId>
```

