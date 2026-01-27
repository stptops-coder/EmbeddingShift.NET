# EmbeddingShift ConsoleEval CLI – Full Guide (code-synchronous)

As of: 2026-01-27

This guide prioritizes **accuracy**: global flags/usage lines are taken from `help` and/or derived directly from the command implementations.

---

## A) Global flags (from `help`)

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
- `run-smoke <refsPath> <queriesPath> <dataset> [--force-reset] [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive] [--sim] [--baseline]`
- `ingest-dataset <refsPath> <queriesPath> <dataset> [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive]`
- `ingest-inspect <dataset> [--role=refs|queries]`
- `dataset-status <dataset> [--role=refs|queries|all]`
- `dataset-reset <dataset> [--role=refs|queries|all] [--force] [--keep-manifests]`
- `dataset-validate <dataset> [--role=refs|queries|all] [--require-state] [--require-chunk-manifest]`

---

## C) Runs (usage/default blocks taken from the respective command classes)

### runs-compare

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
- `domain mini-insurance pipeline [--no-learned]`
- `domain mini-insurance posneg-train --mode=micro|production [--hardneg-topk=N]`
- `domain mini-insurance posneg-run [--latest] [--scale=<float>]`
- `domain mini-insurance posneg-inspect | posneg-history [maxItems] [--include-cancelled] | posneg-best [--include-cancelled]`
- `domain mini-insurance runroot-summarize [--runroot=<path>] [--out=<path>]`

**Important (sync note):** The domain help mentions `--cancel-epsilon=<float>`, but in the current state it is **not** parsed (the flag has no effect).

---

## E) Adaptive/Generator (status and positioning)

- `adaptive` is currently a **demo** (synthetic vectors, local selection) and is not part of the production-like ingest→eval→runs→promote flow.
- The “generator” (no-shift/additive/multiplicative selection) exists as a demo in `EmbeddingShift.ConsoleSmoke` and is not wired into ConsoleEval workflows.
