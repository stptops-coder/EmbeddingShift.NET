# CLI Metrics & Evaluation Artifacts

Reviewed against the implementation on 2026-10-10.

## 1) Two evaluation paths

| Path | Inputs and relevance | Main artifacts |
| --- | --- | --- |
| Mini-Insurance retrieval / PosNeg | Policies and queries, with one relevant policy per query | `run.json`, `eval.perQuery.*.json`, `metrics-posneg.json` |
| Generic `eval` / smoke demo | Persisted query and reference vectors; default relevant reference index is zero for every query | `run-summary.json`, `run_manifest.json`, optional `acceptance_gate.json` |

These paths answer different questions. The generic diagnostic evaluators do not
read the per-query gold document IDs used by the Mini-Insurance retrieval runner.
Their ranking scores are meaningful as retrieval quality only when their configured
relevant indices actually describe the reference set for each query.

PosNeg training and selection currently use the same configured queries. There is
no automatic train/test split. Improvements on these queries are **in-sample**;
they do not establish generalization to new queries or pharmaceutical data.
Simulated embeddings demonstrate the workflow without live embedding API calls.

The canonical Mini-Insurance pipeline also contains two different embedding setups:
First/Delta runs use fixed insurance keyword-count vectors; PosNeg uses the selected
simulation algorithm. The normal sweep comparison selects among the First/Delta
runs. PosNeg records are separate under `_repo`; including them is optional and
changes the compared experiment. Do not interpret that comparison as isolating
only the effect of a query shift on one shared embedding space.

## 2) Retrieval metrics and historical names

Mini-Insurance ranks policy vectors by cosine similarity. Each effective query
has a single relevant policy. Higher MRR and NDCG@3 scores are better.

### Mean reciprocal rank (MRR)

For a relevant policy at one-based rank `r`, reciprocal rank is `1/r`;
it is zero if the policy is not found. MRR is the mean of these values.

**Compatibility:** the historical JSON/CLI metric key `map@1` stores MRR,
not average precision truncated at rank one. Likewise, `PerQueryEval.Ap1`
stores reciprocal rank, and properties such as `MapBaseline`, `MapPosNeg`,
`SelectionMapAt1Baseline` and `SelectionMapAt1Shifted` retain their historical
names. Existing files and `--metric map@1` remain compatible.

A score of 1 means every relevant policy is first. A score of 0.5 can arise
when every relevant policy is second, but also from other mixtures of ranks.
The reciprocal of MRR is **not** the mean rank.

### NDCG@3

With one relevant policy, ideal DCG is 1. The per-query score is
`1 / log2(r + 1)` when `1 <= r <= 3`, otherwise zero. The reported score is
the average across effective queries.

| Relevant policy rank | Reciprocal rank | NDCG@3 |
| --- | ---: | ---: |
| 1 | 1 | 1 |
| 2 | 0.5 | approximately 0.631 |
| 3 | approximately 0.333 | 0.5 |
| 4 | 0.25 | 0 |

Deltas are always `shifted - baseline`. Positive values mean improvement
on the evaluated queries. Compare only runs with matching inputs, embedding
configuration and relevance definitions; a shared metric name alone is insufficient.

## 3) Artifact locations

`EMBEDDINGSHIFT_ROOT=<root>` relocates `data/` and `results/` together.
`EMBEDDINGSHIFT_DATA_ROOT` can independently override the data directory.
Mini-Insurance uses the domain `insurance`. With tenant layout and a tenant,
the domain results directory is `results/insurance/tenants/<tenant>/`;
without a tenant it is `results/insurance/`.

Relative to that domain results directory:

| Artifact | Location |
| --- | --- |
| First/Delta workflow reports and comparable runs | `runs/<workflow>_<timestamp>/` |
| Human-readable PosNeg run and per-query files | `runs/mini-insurance-posneg-run_<timestamp>/` |
| PosNeg baseline run record | `runs/_repo/MiniInsurance-PosNeg-Baseline/<RunId>/run.json` |
| PosNeg shifted run record | `runs/_repo/MiniInsurance-PosNeg/<RunId>/run.json` |
| Training result and Markdown report | `training/<workflow>-training_<timestamp>/shift-training-result.json` and `.md` |
| Generated datasets | `datasets/<name>/stage-<nn>/` |

Training readers also accept the older layout directly beneath the domain results
directory. Generic evaluation creates timestamped folders under the resolved domain
`runs/` directory; `EMBEDDINGSHIFT_RESULTS_DOMAIN` can override its default domain.

Normal run discovery skips `_repo`. For an explicit PosNeg comparison, point
`runs-compare --runs-root` at the appropriate `_repo` subtree. The optional
`--include-repo-posneg` switch on decide/promote adds the shifted PosNeg candidate
source. Do not mix scores from different embedding/relevance configurations merely
because discovery finds them under the same root.

## 4) Comparable run records

The `WorkflowRunArtifact` record persisted in `run.json` contains:

- `RunId` and `WorkflowName`
- `StartedUtc` and `FinishedUtc`
- `Success`
- `Metrics` (string-to-double dictionary)
- `Notes`

JSON casing depends on the writer; readers accept property names case-insensitively.
There are no dedicated `Tenant` or `Labels` properties in this record.
Metric lookup for run comparison is case-insensitive.

Run selection and promotion consider successful runs containing the requested
metric. Higher scores win; ties are ordered by finish time and then run ID.
A successful run is not necessarily better than its baseline: that requires
comparison under matching experimental conditions.

When captured, `run_request.json` records the CLI request and selected environment
settings for `runs-rerun`. It is not an immutable snapshot of the input data.
Retain the original inputs and use a clean environment; uncaptured settings in the
current shell are not cleared automatically. Old request files do not gain settings
that were absent when they were written.

## 5) Per-query artifacts and segment comparison

`eval.perQuery.baseline.json` and `eval.perQuery.posneg.json` contain:

- `QueryId`, `RelevantDocId`
- `Rank` (one-based; zero when not found)
- `Ap1` (historical name for reciprocal rank), `Ndcg3`
- `TopDocId`, `TopScore`, and further top-hit fields when available

Cosine scores range from -1 to 1, subject to numerical precision; they are not
probabilities. In particular, the SHA-based simulation can produce negative scores.

`segment-compare` consumes an **externally produced** decision file:
`Metric`, `Eps`, `VariantAPath`, `VariantBPath`, and a `Decisions` dictionary mapping
query IDs to `VariantA` or `VariantB`. Legacy paths `BaselinePath` / `PosNegPath`
and decisions `SkipShift` / `ApplyShift` remain supported.

It reports mean reciprocal rank and NDCG@3 for both variants and the selected mix,
plus selection counts. `Eps` is informational in this comparison. Better scores for
the mix describe the supplied decisions on the evaluated queries; if those decisions
were derived from the same gold labels, they are not evidence of a deployable router.

## 6) Generic eval diagnostics and acceptance gate

Default evaluators are:

| Evaluator | Meaning |
| --- | --- |
| `CosineSimilarityEvaluator` | Mean cosine similarity to all references |
| `MarginEvaluator` | Difference between the two highest cosine scores |
| `NdcgEvaluator` | Binary nDCG@10, relevant indices `{0}` by default |
| `MrrEvaluator` | Reciprocal rank of reference index zero by default |

Higher mean cosine or margin does not by itself prove that the correct document
was retrieved. These are diagnostic measures with the relevance limitations above.

Without baseline comparison, metrics use evaluator names and duration keys.
With baseline comparison, each evaluator emits `.baseline`, `.shift`, and `.delta`.
`run-summary.json` persists the numeric summary on a best-effort basis; verify that
the file exists if an external process depends on it.

When baseline mode has an active gate, `acceptance_gate.json` records its result.
The default `rank` profile requires both `NdcgEvaluator.delta` and
`MrrEvaluator.delta` to be at least `-epsilon`, with default `epsilon = 1e-6`.
The `rank+cosine` profile additionally applies the same threshold to
`CosineSimilarityEvaluator.delta`. Missing required metrics fail the gate.
This gate checks regression against the baseline, not absolute retrieval quality.

## 7) Training results and best-candidate selection

`shift-training-result.json` contains workflow/time metadata, `BaseDirectory`,
`ComparisonRuns`, `ScopeId`, `TrainingMode`, `DeltaVector`, `DeltaNorm`,
`CancelOutEpsilon`, `IsCancelled` and `CancelReason`.

PosNeg also persists baseline/shifted MRR and NDCG@3 selection metrics and:

`SelectionScore = 0.7 * (MRR_shifted - MRR_baseline) + 0.3 * (NDCG3_shifted - NDCG3_baseline)`

`LoadBest` excludes cancelled results by default and prefers higher selection scores.
For older records without `SelectionScore`, it uses `ImprovementFirstPlusDelta`
when its absolute value is at least `1e-12`, otherwise `ImprovementFirst`.
Ties within the repository's tolerance prefer newer `CreatedUtc` values.
`LoadLatest` chooses by recency and is a distinct operation.

Selection does not enforce matching dataset contents or embedding configurations.
Use a fresh experiment root when changing those inputs. A matching vector dimension
or workflow name does not establish compatibility of learned vectors.

The legacy improvement fields and `DeltaImprovement` remain for First/Delta
compatibility. Training mode values are `Micro` / `Production`; currently the mode
is recorded without automatically changing dataset size or creating a holdout split.
Detailed learner diagnostics such as clipping and individual direction statistics
are printed to the console and are not all persisted in the training record.

## 8) Source references

- [Metric keys](../src/EmbeddingShift.Core/Stats/MetricKeys.cs)
- [PosNeg runner](../src/EmbeddingShift.ConsoleEval/MiniInsurancePosNegRunner.cs)
- [PosNeg trainer](../src/EmbeddingShift.ConsoleEval/MiniInsurancePosNegTrainer.cs)
- [Training selection scoring](../src/EmbeddingShift.Abstractions/Shifts/ShiftTrainingResultScoring.cs)
- [Generic evaluation runner](../src/EmbeddingShift.Core/Evaluators/EvaluationRunner.cs)
- [Acceptance gate](../src/EmbeddingShift.Workflows/Workflows/Eval/EvalAcceptanceGate.cs)
