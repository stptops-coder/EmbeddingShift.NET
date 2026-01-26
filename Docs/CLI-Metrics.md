# CLI Metrics & Evaluation Artifacts (Code-synchronous, as of 2026-01-26)

This document describes **which metrics/statistics** the CLI currently produces, **where** they are stored, and **how** to interpret them – focusing on the "production-like" workflow (run artifacts + comparisons), not older First-Delta experiments.

---

## 0) Where do metrics originate?

In the repo there are currently **multiple layers** of metrics/outputs that must be kept separate:

1) **Run artifacts (comparable/aggregatable)**
   - Persisted as `run.json` inside a run folder.
   - Read by `runs-compare`, `runs-active`, `runs-history`, `runs-matrix`, etc.
   - Currently used primarily by the **Mini-Insurance PosNeg run**.

2) **Per-query breakdown (for segmentation/analysis)**
   - Persisted as JSON with *per query* rank/AP/NDCG.
   - Used by `segment-compare`.

3) **Eval runner (diagnostic evaluators, optionally baseline/delta)**
   - Produces metrics such as `NdcgEvaluator.delta`, `MrrEvaluator.delta`.
   - Optionally writes an `acceptance_gate.json` (pass/fail + metrics).
   - Important: these evaluators operate with a **shared reference set** (not a per-query gold-label set).

4) **Ingest/manifests (counts/lineage, not “KPIs” in the narrow sense)**
   - Manifests contain counts (docs/chunks/embedding-dim) and lineage.

---

## 1) Paths / layout (where to find the files)

Root directories are resolved via `DirectoryLayout`:

- **`EMBEDDINGSHIFT_ROOT`** (optional): forces a stable root.
- Without env var: the system attempts `repo-root/results` and `repo-root/data` (fallbacks: current directory, BaseDirectory).
- **`EMBEDDINGSHIFT_TENANT`** (optional): enables tenant scoping.

### 1.1 Mini-Insurance (run and training artifacts)

For Mini-Insurance the domain key is **`insurance`**.

Typical layout (with tenant):

```
results/
  insurance/
    tenants/<tenant>/
      runs/
        MiniInsurance-PosNeg-Baseline/
          <RunId>/
            run.json
        MiniInsurance-PosNeg/
          <RunId>/
            run.json

      mini-insurance-posneg-run_YYYYMMDD_HHMMSS_fff/
        eval.perQuery.baseline.json
        eval.perQuery.posneg.json
        metrics-posneg.json
        metrics-posneg.md

      mini-insurance-posneg-training_YYYYMMDD_HHMMSS_fff/
        shift-training-result.json
        shift-training-result.md
```


Without tenant, the `tenants/<tenant>` part is missing.

### 1.2 Eval runner (global, timestamped)

The eval runner produces timestamped folders under `results/` (tenant-aware if `EMBEDDINGSHIFT_TENANT` is set), e.g.

```
results/
  tenants/<tenant>/
    20260125_123456_evaluation+baseline_<runId>/
      run_manifest.json
      acceptance_gate.json   (only when baseline-mode + gate is active)
```

Note: the actual numeric metrics are primarily printed **to the console**; the most structured persisted output is currently `acceptance_gate.json`.

---

## 2) Run artifact `run.json` (the central KPI source)

A run artifact is a JSON document with this schema (simplified):

- `RunId` (GUID)
- `Workflow` (string)
- `Tenant` (string)
- `StartedAtUtc`, `CompletedAtUtc`
- `Labels` (key/value)
- `Metrics` (key/value, double)

### 2.1 Active KPI keys (Mini-Insurance PosNeg)

Currently, the PosNeg run persists two KPI metrics:

- **`map@1`**
- **`ndcg@3`**

These keys are **case-insensitive** and can be used in `runs-compare` via `--metric <key>`.

---

## 3) Retrieval KPIs (production-like): `map@1` and `ndcg@3`

These metrics come from the Mini-Insurance PosNeg runner (retrieval experiment):

- documents = policies (embeddings per policy)
- queries = query embeddings
- ranking = cosine similarity (query vs policy)
- exactly **one** relevant document per query (single-label setup)

### 3.1 MAP@1 (`map@1`)

Implementation (single relevant doc):

- For each query, determine the **rank r (1-based)** of the relevant document.
- Per query:
  - `AP@1 = 1 / r`
- `MAP@1 = average(AP@1)` over all effective queries.

Interpretation:
- **1.0**: relevant doc is always ranked #1.
- **0.5**: relevant doc is on average ranked #2.
- **0.33**: relevant doc is on average ranked #3.
- Higher is better.

### 3.2 NDCG@3 (`ndcg@3`)

Implementation (single relevant doc, cutoff K=3):

- `DCG@3 = 1 / log2(r + 1)` if `r <= 3`, else `0`.
- `IDCG@3 = DCG@3(r=1) = 1 / log2(2) = 1`.
- `NDCG@3 = DCG@3 / IDCG@3`.

Therefore (single-label):
- rank 1 → **1.0**
- rank 2 → **~0.631**
- rank 3 → **0.5**
- rank >3 → **0**

Interpretation:
- Emphasizes top-3 quality.
- Higher is better.

### 3.3 Delta interpretation

In the PosNeg run, the CLI prints (to the console) additional deltas:

- `Delta MAP@1 = MAP@1_shifted - MAP@1_baseline`
- `Delta NDCG@3 = NDCG@3_shifted - NDCG@3_baseline`

Positive delta = improvement over baseline.

---

## 4) Per-query artifacts (basis for segmentation)

### 4.1 Files

The PosNeg run writes two per-query files into the run folder:

- `eval.perQuery.baseline.json`
- `eval.perQuery.posneg.json`

Both contain a list of `PerQueryEval` records.

### 4.2 `PerQueryEval` fields

Per query, at least the following fields are persisted:

- `QueryId` – query ID
- `RelevantDocId` – expected relevant document
- `Rank` – 1-based rank of the relevant document
- `Ap1` – `1/Rank`
- `Ndcg3` – NDCG@3 (see above)
- `TopDocId`, `TopScore` – best ranked doc and its cosine score
- optional: `Top2DocId`, `Top2Score` – second best doc and score

Important:
- `TopScore` and `Top2Score` are cosine similarities (typically in [0..1]).
- `Rank` is 1-based.

---

## 5) Segment-compare (segmentation: apply vs skip)

`segment-compare` is a pure **analysis command**.
It requires a *segments file* (JSON) that is produced **externally**.

### 5.1 Segments file schema

The segments file contains:

- `Metric` (e.g. `ndcg@3` or `map@1`)
- `Eps` (threshold; informational in compare)
- `BaselinePath` (path to `eval.perQuery.baseline.json`)
- `PosNegPath` (path to `eval.perQuery.posneg.json`)
- `Decisions` (dictionary `QueryId -> ApplyShift|SkipShift`)

### 5.2 Output statistics

For **effective queries**, `segment-compare` computes:

- Baseline KPI: avg MAP@1, avg NDCG@3
- PosNeg KPI: avg MAP@1, avg NDCG@3
- Segmented KPI: avg MAP@1, avg NDCG@3 (apply/skip per query)
- Counts: apply vs skip

Interpretation:
- If the segmented KPI is better than both baseline and posneg, the segmentation decision is doing useful work.

---

## 6) Eval runner metrics (diagnostic)

These are produced by the CLI eval runner (not the PosNeg retrieval runner).
Relevant classes include:

- `CosineSimilarityEvaluator`
- `MarginEvaluator`
- `NdcgEvaluator` (default: relevant indices = `{0}`, `k=10`)
- `MrrEvaluator` (default: relevant index = `0`)

### 6.1 Key limitation

These evaluators always receive the **same reference embedding set** for every query.
This is **not** the same setup as the retrieval run, where each query has its own gold document.

Consequences:
- `CosineSimilarityEvaluator` and `MarginEvaluator` are robust *alignment heuristics*.
- `NdcgEvaluator`/`MrrEvaluator` are only meaningful if your reference set is constructed such that the relevant indices (default: 0) truly represent “gold”.

### 6.2 Metric keys

**Without baseline**, the logger emits:

- `<EvaluatorName>` (avg score across all queries)
- `<EvaluatorName>.duration_ms`
- `evaluation.duration_ms`

**With baseline** (NoShift), additionally:

- `<EvaluatorName>.baseline`
- `<EvaluatorName>.shift`
- `<EvaluatorName>.delta` (= shift - baseline)

Examples:
- `NdcgEvaluator.delta`
- `MrrEvaluator.delta`

### 6.3 Meaning (brief)

- **CosineSimilarityEvaluator**
  - Score = mean cosine similarity of `Shift(query)` to *all* references.
  - Higher is better.

- **MarginEvaluator**
  - Score = (top1 cosine) - (top2 cosine) over references.
  - Higher = clearer decision/separation.

- **NdcgEvaluator**
  - nDCG@K with binary relevance, default K=10.
  - Relevant indices default to `{0}` only.

- **MrrEvaluator**
  - Mean reciprocal rank for the relevant index (default 0).

---

## 7) Acceptance gate (pass/fail + persisted metrics)

If the eval run is executed in baseline mode and a gate is active, an `acceptance_gate.json` is written.

Default rules in the gate:

- `NdcgEvaluator.delta >= -0.005`
- `MrrEvaluator.delta >= -0.005`
- optionally additionally:
  - `CosineSimilarityEvaluator.delta >= 0.0`

Interpretation:
- The gate is currently conservative: it allows minimal regression, but prevents substantial degradation.
- The gate file is currently the **most reliable persisted source** for eval runner metrics.

---

## 8) Shift training result (`shift-training-result.json`)

Training results are stored via `FileSystemShiftTrainingResultRepository`.
Important: “Best” is currently **score-based**, but for PosNeg the improvement fields are currently 0 → `LoadBest` effectively behaves like “latest”.

### 8.1 Persisted fields

- `WorkflowName`
- `CreatedUtc`
- `BaseDirectory`
- `ComparisonRuns`
- `TrainingMode` (e.g. `posneg-uniform`)
- `CancelOutEpsilon`
- `IsCancelled`, `CancelReason`
- `DeltaVector` (float[])
- `DeltaNorm` (L2 norm of DeltaVector)

Legacy/First-Delta fields (for PosNeg currently not meaningfully used):

- `ImprovementFirst`
- `ImprovementFirstPlusDelta`
- `DeltaImprovement`

Additionally, `shift-training-result.md` is generated (readability + top dimensions by |value|).

---

## 9) PosNeg learning statistics (console-only)

During `posneg-train`, diagnostic values are computed (e.g., norm clipping, zero directions, cancel-out analysis).
These are currently **not** fully persisted in JSON.

If you need these values for production gating, the next step would be:

- Persist structured `PosNegDeltaVectorLearnerStats` into `ShiftTrainingResult` or write a separate `training_stats.json`.

---

## 10) Quick mapping: which metric for which decision?

**For production-like KPIs (retrieval quality):**
- `ndcg@3` (top-3 focus) and `map@1` (rank sensitivity)
- Source: `run.json` (run artifacts)

**For segmentation PoC (apply/skip per query):**
- `eval.perQuery.*.json` + `segment-compare` output

**For “shift is not worse than baseline” gating:**
- `acceptance_gate.json` (eval runner + baseline)

**For shift learning stability diagnostics:**
- cancel-out / norm clipping / direction stats (currently console-only)
