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

As of: 2026-01-27

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
