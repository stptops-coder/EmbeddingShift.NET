## Workflow overview: runs, comparison, and “what is good”

Most commands in this CLI boil down to producing **runs** that can be compared:

- A run always uses a **dataset scope** and produces file-based artifacts (inputs, embeddings, and metrics).
- **Baseline** runs measure retrieval quality without a learned shift.
- **Shift** runs re-evaluate retrieval after applying a shift (trained or configured).
- “Good” is defined by **metric improvement** (commonly NDCG@K / MRR@K) under the same evaluation setup.

If you only remember one thing: keep the *evaluation setup constant* (same dataset/scope/queries) and compare runs by metrics.
Metric definitions live in `Docs/CLI-Metrics.md`.

### Pos/Neg in one paragraph

Pos/Neg training derives a directional shift from positive vs. negative examples. Instead of changing the model, it adjusts embeddings so that positives tend to move closer and negatives farther away in retrieval space.


# EmbeddingShift ConsoleEval CLI – Full Guide (code-synchronous)

As of: 2026-02-03

This guide prioritizes **accuracy**: global flags/usage lines are taken from `help` and/or derived directly from the command implementations.


This guide is a **reference**. All commands assume your current directory is the **repository root** (the folder that contains `src/` and `scripts/`). For runnable copy/paste sequences, start with `Docs/CLI-Onboarding.md`.

---

## A) Global flags (from `help`)


> **Note on help:** The CLI only supports `help` / `--help` at the top-level (and `domain <id> help`). `--help` placed after a subcommand is treated as unsupported nested help and prints the parent help.

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

---

### Environment variables (optional)

The CLI supports a small set of `EMBEDDINGSHIFT_*` environment variables to keep runs reproducible and paths stable across machines/sweeps.

**Common (file layout / scoping):**
- `EMBEDDINGSHIFT_ROOT` — forces a stable root for `results/` and `data/` (otherwise repo-root fallbacks are used).
- `EMBEDDINGSHIFT_TENANT` — tenant key; enables tenant-scoped results under `results/<domain>/tenants/<tenant>/...`.
- `EMBEDDINGSHIFT_RESULTS_DOMAIN` — optional override for `<domain>` (default is `insurance`).
- `EMBEDDINGSHIFT_DATA_ROOT` — optional override for the data root (rare; mostly for experiments).
- `EMBEDDINGSHIFT_REPO_ROOT` — optional override for repo-root detection (useful in scripted runs).

**Mini-Insurance domain pack:**
- `EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT` — points at a staged dataset root (e.g. `...\datasets\MyDS\stage-00`).

**Debug / test toggles (use only when you know why):**
- `EMBEDDINGSHIFT_POSNEG_DEBUG`, `EMBEDDINGSHIFT_POSNEG_NOCLIP`, `EMBEDDINGSHIFT_POSNEG_DISABLE_CLIP`
- `EMBEDDINGSHIFT_ACCEPTANCE_KEEP_ARTIFACTS`

**PowerShell example (session-scoped):**
```powershell
$env:EMBEDDINGSHIFT_ROOT   = "C:\temp\EmbeddingShift.Sweep\20260131_012048"
$env:EMBEDDINGSHIFT_TENANT = "insurer-a"
$env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = "results\insurance\tenants\insurer-a\datasets\SweepDS\stage-00"
```

---

## B) Dataset/Ingest/Eval (usage lines from `DatasetCliCommands`)

- `run <refsPath> <queriesPath> <dataset> [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive] [--sim] [--baseline] [--shift=identity|zero] [--gate-profile=rank|rank+cosine] [--gate-eps=1e-6]`
  - Purpose: End-to-end command: ingest (if needed) → embed → retrieve → metrics; supports baseline/shift and simple dataset gates.
- `run-smoke <refsPath> <queriesPath> <dataset> [--force-reset] [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive] [--sim] [--baseline]`
  - Purpose: Fast sanity run with safe defaults (ingest → validate → eval). Good for first checks and repeatability.
- `ingest-dataset <refsPath> <queriesPath> <dataset> [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive]`
  - Purpose: Create/refresh dataset artifacts (manifests, chunking) without running evaluation.
- `ingest-inspect <dataset> [--role=refs|queries]`
  - Purpose: Inspect dataset artifacts (counts/samples) for refs or queries.
- `dataset-status <dataset> [--role=refs|queries|all]`
  - Purpose: Show current dataset status (what exists) for refs/queries/all.
- `dataset-reset <dataset> [--role=refs|queries|all] [--force] [--keep-manifests]`
  - Purpose: Reset dataset artifacts for a clean rerun (optionally keep manifests).
- `dataset-validate <dataset> [--role=refs|queries|all] [--require-state] [--require-chunk-manifest]`
  - Purpose: Validate required artifacts and invariants; used as a gate before running evaluations.

---
## C) Runs (usage/default blocks taken from the respective command classes)

Each persisted run lives in its own timestamped folder under the runs root and contains:

- `run.json` — metrics + minimal metadata (used for compare/best/decide/promote)
- `run_request.json` — optional replay snapshot (only written when a RunRequestContext is present)
- `report.md` — human-readable report


### Run activation lifecycle (compare → best → decide → promote/rollback)

The CLI supports a small “activation loop” that lets you select a winner by metric and persist an **active pointer**:

- `runs-compare` ranks candidates (`run.json`) by a metric (e.g., `ndcg@3`).
- `runs-best` writes a best pointer under `runs/_best/` (e.g., `best_ndcg@3.json`).
- `runs-decide` applies an epsilon gate and writes a decision record under `runs/_decisions/`.
- `runs-promote` writes the active pointer under `runs/_active/` and archives history.
- `runs-rollback` restores the last archived active pointer.

**Rerun** (`runs-rerun`) is a *verification tool*: if a run captured `run_request.json`, it can replay the original command. By default, rerun is **manual / operator-triggered** (or a higher-level controller could trigger it after `runs-decide`).

### Dataset stages as “patch levels”

The Mini-Insurance generator can emit multiple stages (e.g., `stage-00`, `stage-01`, `stage-02`). Treat these stages as **dataset versions / patch levels**:

- A promotion decision should always be interpreted **within the same stage** (same dataset root, same evaluation setup).
- If you advance to a new stage, you are effectively evaluating on a changed corpus; expect metrics to move and re-run the activation loop.

A practical rule: *keep (Tenant, DatasetName, Stage, Seed, Metric) stable while comparing runs.*

---

### runs-compare

Purpose: Rank and compare runs by a chosen metric (top-N), optionally writing a small report.

```
Usage:
runs-compare [--runs-root=<path>] [--domainKey=<key>] [--metric=<key>] [--top=N] [--write] [--out=<dir>]

Defaults:
domainKey = insurance
tenant    = ENV:EMBEDDINGSHIFT_TENANT (or "insurer-a" if missing)
runs-root = .\results\<domainKey>\tenants\<tenant>\runs
metric    = ndcg@3
top       = 20
write     = false
```

### runs-best

Purpose: Select the best run for a metric and optionally persist/write the selection for later use.

```
Usage:
runs-best [--runs-root=<path>] [--domainKey=<key>] [--metric=<key>] [--write] [--out=<dir>] [--open]

Defaults:
domainKey = insurance
tenant    = ENV:EMBEDDINGSHIFT_TENANT (or "insurer-a")
runs-root = .\results\<domainKey>\tenants\<tenant>\runs
metric    = ndcg@3
```

### runs-decide

Purpose: Apply a simple acceptance gate (eps threshold) to decide whether a candidate run is acceptable; can write/apply the decision.

```
Usage:
runs-decide [--runs-root=<path>] [--domainKey=<key>] [--metric=<key>] [--eps=<double>] [--write] [--apply] [--open]

Defaults:
domainKey = insurance
tenant    = ENV:EMBEDDINGSHIFT_TENANT (or "insurer-a")
runs-root = .\results\<domainKey>\tenants\<tenant>\runs
metric    = ndcg@3
eps       = 1e-6
write     = true
apply     = false (dry decision only)

Notes:
- 'apply' uses RunActivation.Promote(...) and will create/overwrite the active pointer for this metric.
- This command does not delete any run directories.
```

### runs-promote

Purpose: Promote the currently selected/best run to become the active run for a metric.

```
Usage:
runs-promote [--runs-root=<path>] [--domainKey=<key>] [--metric=<key>] [--open]

Defaults:
domainKey = insurance
tenant    = ENV:EMBEDDINGSHIFT_TENANT (or "insurer-a")
runs-root = .\results\<domainKey>\tenants\<tenant>\runs
metric    = ndcg@3
```

### runs-rollback

Purpose: Rollback the active run pointer to the previous state (undo the last promotion).

```
Usage:
runs-rollback [--runs-root=<path>] [--domainKey=<key>] [--metric=<key>] [--open]

Defaults:
domainKey = insurance
tenant    = ENV:EMBEDDINGSHIFT_TENANT (or "insurer-a")
runs-root = .\results\<domainKey>\tenants\<tenant>\runs
metric    = ndcg@3
```

### runs-active

Purpose: Show which run is currently marked active for a metric (and where it lives).

```
Usage:
runs-active [--runs-root=<path>] [--domainKey=<key>] [--metric=<key>]

Defaults:
domainKey = insurance
tenant    = ENV:EMBEDDINGSHIFT_TENANT (or "insurer-a")
runs-root = .\results\<domainKey>\tenants\<tenant>\runs
metric    = ndcg@3
```

### runs-rerun

Purpose: Replay the CLI command that produced a run, based on `run_request.json`.

```
Usage:
runs-rerun --run-dir=<path> [--print] [--keep-env]

Defaults:
print    = false
keep-env = false
```

Notes:
- By default, `runs-rerun` applies the captured environment snapshot (EMBEDDING_* vars) before replay.
- `--print` prints the reconstructed command and exits without executing it.



### runs-history

Purpose: List promotion/decision history for a metric and link to stored artifacts (optional open).

```
Usage:
runs-history [--runs-root=<path>] [--domainKey=<key>] [--metric=<key>] [--max=N] [--exclude-preRollback] [--open]

Defaults:
domainKey = insurance
tenant    = ENV:EMBEDDINGSHIFT_TENANT (or "insurer-a")
runs-root = .\results\<domainKey>\tenants\<tenant>\runs
metric    = ndcg@3
max       = 20
```

### runs-matrix

Purpose: Execute a predefined set of run variants from a spec file and compare them systematically.

```
Usage:
runs-matrix --spec=<path> [--runs-root=<path>] [--domainKey=<key>] [--dry] [--open]

The spec file contains the list of variants (each variant is a CLI argument array for this ConsoleEval app)
plus optional "after" settings (compare/promote/open).
```

Important clarifications (to avoid “implicitly wrong” documentation):

- There is **no** `--workflow` option on `runs-compare`. If you want to scan a single workflow only: point `--runs-root` directly at that workflow folder.
- `runs-promote`/`runs-rollback` work via “best/previous” per metric – there is currently no explicit `runId` promotion.
- `runs-matrix` has **no** `--out`/`--rerun`. Output is automatic: `<runsRoot>/_matrix/matrix_<timestamp>/`.

---

## D) Mini-Insurance domain pack (key subcommands)

- `domain mini-insurance dataset-generate <name> [--stages=N] [--policies=N] [--queries=N] [--seed=N] [--overwrite]`
  - Purpose: Generate a deterministic synthetic Mini-Insurance dataset (refs + queries) for demos/tests.
- `domain mini-insurance pipeline [--no-learned]`
  - Purpose: Run a default end-to-end workflow for Mini-Insurance (baseline → shifts → eval) as a compact demo.
- `domain mini-insurance posneg-train --mode=micro|production [--hardneg-topk=N]`
  - Purpose: Train a Pos/Neg global delta (micro = small debug run, production = fuller run; optional hard-negative top-K).
- `domain mini-insurance posneg-run [--latest] [--scale=<float>]`
  - Purpose: Run evaluation with a Pos/Neg shift applied (latest/best), with optional scale.
- `domain mini-insurance posneg-inspect | posneg-history [maxItems] [--include-cancelled] | posneg-best [--include-cancelled]`
  - Purpose: Inspect Pos/Neg training results (latest/history/best variants) and related artifacts.
- `domain mini-insurance runroot-summarize [--runroot=<path>] [--out=<path>]`
  - Purpose: Create a compact summary report for a runroot folder (shareable/diff-friendly).

**Important (sync note):** The domain help mentions `--cancel-epsilon=<float>`, but in the current state it is **not** parsed (the flag has no effect).

---
## E) Adaptive/Generator (status and positioning)

- `adaptive` is currently a **demo** (synthetic vectors, local selection) and is not part of the production-like ingest→eval→runs→promote flow.
- The “generator” (no-shift/additive/multiplicative selection) exists as a demo in `EmbeddingShift.ConsoleSmoke` and is not wired into ConsoleEval workflows.
