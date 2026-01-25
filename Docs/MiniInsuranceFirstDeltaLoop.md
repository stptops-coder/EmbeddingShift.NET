# Mini-Insurance First/Delta Loop

This document summarizes the end-to-end mini-insurance workflow around
FirstShift / DeltaShift and the learned Delta candidate. It is meant as
a reference implementation and a template that can be ported to other domains.

---

## 1. Goals

- Provide a **small, fully deterministic playground** for shift-based
  embedding adaptation (FirstShift, DeltaShift, learned Delta).
- Show a complete loop:

  > Runs → Metrics → Aggregation → Training → Candidate → Inspect → Learned run

- Keep all artifacts **file-based and JSON/Markdown-based**, with clean abstractions
  so alternative storage can be added later if needed (out of scope for now).
---

## 2. Data & Embedding Space

- Documents: `samples/insurance/policies/*.txt`
- Queries: `samples/insurance/queries/queries.json`
  - 5 queries, some of which are **intentionally mis-ranked** by the baseline.
- Embedding space:
  - Simulated keyword-count embedding in a **1536-dimensional** vector.
  - The first 7 dimensions represent insurance-related keywords:
    - `[0] fire`
    - `[1] water`
    - `[2] damage`
    - `[3] theft`
    - `[4] claims`
    - `[5] flood`
    - `[6] storm`
  - Remaining dimensions are zero in the mini playground.

Shifts operate in the same 1536D space but only touch a small subset of
dimensions (mainly 5 and 6 for flood/storm).

---

## 3. Core Components

### 3.1 Workflows

- `FileBasedInsuranceMiniWorkflow`
  - Baseline (no shifts).
  - Overloads to inject:
    - `CreateFirstShiftPipeline()`
    - `CreateFirstPlusDeltaPipeline()`
    - `CreateFirstPlusDeltaPipeline(float[] deltaVector)` (learned Delta).

- `StatsAwareWorkflowRunner`
  - Executes workflows.
  - Produces `WorkflowResult` including:
    - Per-query details (internally).
    - Metrics (e.g., `map@1`, `ndcg@3`).

---

## 4. Commands (ConsoleEval)

All commands are run from the repository root:

~~~bash
dotnet run --project src/EmbeddingShift.ConsoleEval -- <command>
~~~

### 4.1 Baseline vs First vs First+Delta

~~~bash
dotnet run --project src/EmbeddingShift.ConsoleEval -- mini-insurance-first-delta
~~~

- Runs three workflows:
  - Baseline
  - FirstShift
  - First+handcrafted Delta
- Persists each run under:

  - `results/insurance/FileBased-Insurance-Mini-Baseline_<timestamp>/`
  - `results/insurance/FileBased-Insurance-Mini-FirstShift_<timestamp>/`
  - `results/insurance/FileBased-Insurance-Mini-FirstShift-Delta_<timestamp>/`

  Each run directory contains:
  - `report.md`
  - `run.json`

  Plus a comparison directory:

    - `results/insurance/mini-insurance-first-delta_<timestamp>/metrics-comparison.json`
    - `results/insurance/mini-insurance-first-delta_<timestamp>/metrics-comparison.md`

- Comparison payload type:
  - `MiniInsuranceFirstDeltaComparison`
  - and per-metric rows: `MiniInsuranceMetricRow`.

### 4.2 Aggregate over many comparison runs

~~~bash
dotnet run --project src/EmbeddingShift.ConsoleEval -- mini-insurance-first-delta-aggregate
~~~

- Scans all `mini-insurance-first-delta_*` directories under
  `results/insurance`.
- Aggregates metrics into:
  - `MiniInsuranceFirstDeltaAggregate`
  - with rows: `MiniInsuranceAggregateMetricRow`
- Persists aggregate under:

  - `results/insurance/mini-insurance-first-delta-aggregate_<timestamp>/`
    - `metrics-aggregate.json`
    - `metrics-aggregate.md`

This represents the **metrics layer** that can later be mapped to a DB
table structure.

### 4.3 Train Delta candidate from metrics

~~~bash
dotnet run --project src/EmbeddingShift.ConsoleEval -- mini-insurance-first-delta-train
~~~

- Reads the latest aggregate.
- Computes:

  - `ImprovementFirst` (combined improvement of First vs baseline).
  - `ImprovementFirstPlusDelta` (combined improvement of First+Delta vs baseline).
  - `DeltaImprovement` (First+Delta vs First).

- Produces a **Delta candidate**:

  - Type: `MiniInsuranceShiftTrainingResult`
  - Contains:
    - Metadata (`CreatedUtc`, `BaseDirectory`, `ComparisonRuns`).
    - Improvement values.
    - `DeltaVector` (1536D).

- Persists under:

  - `results/insurance/mini-insurance-first-delta-training_<timestamp>/`
    - `shift-candidate.json`
    - `shift-candidate.md`

The Delta vector magnitude is **scaled based on observed improvements**
(Trainer v1.1). Flood/storm dimensions get a magnitude derived from
`ImprovementFirstPlusDelta`, clamped into a safe range.

### 4.4 Inspect latest Delta candidate

~~~bash
dotnet run --project src/EmbeddingShift.ConsoleEval -- mini-insurance-first-delta-inspect
~~~

- Uses `MiniInsuranceFirstDeltaCandidateLoader.LoadLatestCandidate(...)`.
- Prints to console:
  - `CreatedUtc`, `BaseDirectory`, `ComparisonRuns`.
  - Improvements (First, First+Delta, Delta vs First).
  - Top-N Delta dimensions (by absolute value).

This is the **primary read-only view** on the learned Delta state.

### 4.5 Run with learned Delta

~~~bash
dotnet run --project src/EmbeddingShift.ConsoleEval -- mini-insurance-first-learned-delta
~~~

- Loads the latest Delta candidate via `MiniInsuranceFirstDeltaCandidateLoader`.
- Constructs:

  - Baseline workflow.
  - FirstShift workflow.
  - First+LearnedDelta workflow (uses `CreateFirstPlusDeltaPipeline(float[])`).

- Persists runs and a new comparison:

  - Baseline / First / First+LearnedDelta.
  - Metrics comparison written again as `MiniInsuranceFirstDeltaComparison`
    under `mini-insurance-first-delta_<timestamp>/`.

In the current Mini-Insurance demo, the learned Delta behaves similarly
to the hand-crafted Delta. The key point is now implemented: the **pipeline
from metrics → learned Delta amplitude**.

---

## 5. Persistence & Repository Abstraction

- Metrics and aggregates are persisted via `IMetricsRepository`.
- Current implementation:
  - `FileSystemMetricsRepository` inside `ConsoleEval`.
  - Writes JSON + Markdown side by side.

The design intentionally stays storage-agnostic:

- `MiniInsuranceFirstDeltaComparison`
  - → comparison table (+ optional details).
- `MiniInsuranceFirstDeltaAggregate`
  - → aggregate table.
- `MiniInsuranceShiftTrainingResult`
  - → candidate record (+ vector payload).

If we ever need a database later, we can implement `IMetricsRepository`
accordingly. For now, the project intentionally stays file-based.

---

## 6. Extension Points

1. **Embeddings / backends**
   - This Mini-Insurance loop uses **local, deterministic keyword-count embeddings** (it does not depend on `EMBEDDING_BACKEND`).
   - For the generic ingest/eval pipeline (chunk → embed → store → eval), see the ConsoleEval dataset/ingest commands, which use:
     - `EMBEDDING_BACKEND=sim` (default)
     - `EMBEDDING_BACKEND=openai` (scaffold only; not wired yet)

2. **Alternative storage (optional)**
   - Keep `IMetricsRepository` and provide a different implementation if needed.
   - Out of scope for the current phase (file-based is the default).

3. **Additional metrics**
   - Extend `WorkflowResult.Metrics` with more KPIs
     (e.g. `recall@k`).
   - They automatically flow into comparison and aggregation.

4. **Multiple domains**
   - Clone this mini-loop for other domains (e.g. medical, finance).
   - Only data and parts of the shift logic change; the pattern remains stable.


## 7. Summary

The mini-insurance setup provides a compact, fully deterministic
reference implementation for:

- Embedding-based retrieval with fixed and learned shifts.
- Metrics calculation and aggregation.
- Training of a small Delta vector based on metric improvements.
- Persisted JSON/Markdown artifacts that are structurally ready to be
  moved into a database.

This loop is intended as a **template** for more complex domains and
real-world embeddings.
