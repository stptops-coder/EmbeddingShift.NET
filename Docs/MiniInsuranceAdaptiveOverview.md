# Mini-Insurance · Adaptive Overview

This document summarizes how the Mini-Insurance domain connects
the statistical / training layer with the adaptive shift selection.

## 1. Baseline and First/Delta

The classic Mini-Insurance path is:

1. **Baseline**  
   - Embeddings for policies and queries are computed via the simulated backend.  
   - Similarity is evaluated without any shift.

2. **FirstShift**  
   - A hand-crafted (or domain-informed) First shift is applied.  
   - We compare Baseline vs First.

3. **First+Delta**  
   - A small Delta shift is added on top of First.  
   - We compare Baseline vs First vs First+Delta and aggregate metrics.

This is orchestrated by:

- `mini-insurance-first-delta`
- `mini-insurance-first-delta-pipeline`
- `mini-insurance-first-delta-aggregate`

## 2. Training a global Delta (Pos/Neg)

In addition to First/Delta, there is a pos/neg training path:

1. We define **positive** and **negative** policy examples per query.
2. The trainer computes a global Delta vector from the differences:
   - For each (query, pos, neg) triple it builds a direction vector.
   - All direction vectors are aggregated into a single Delta.
3. The training result is stored as a `ShiftTrainingResult`:

   - `WorkflowName = "mini-insurance-posneg"`
   - `DeltaVector` (1536 dimensions)
   - Improvements (MAP / NDCG deltas)
   - Scope and metadata

The main entry points are:

- `MiniInsurancePosNegTrainer` → `TrainAsync(EmbeddingBackend.Sim)`
- `MiniInsurancePosNegRunner` → evaluates Baseline vs PosNeg
- `FileSystemShiftTrainingResultRepository` → persists results under  
  `src/EmbeddingShift.ConsoleEval/bin/Debug/net8.0/results/insurance`

You can inspect training results via:

`dotnet run --project src/EmbeddingShift.ConsoleEval -- shift-training-inspect mini-insurance-posneg`

## 3. From training to adaptive shifts

The adaptive layer connects to these training results via:

- `TrainingBackedShiftGenerator` (in `EmbeddingShift.Adaptive`)

This generator:

1. Loads the **latest** `ShiftTrainingResult` for a given workflow name.
2. Reads the `DeltaVector` and normalizes it to the embedding dimension.
3. Exposes it as an `AdditiveShift` (global shift for this workflow).
4. Falls back to `NoShift.IngestBased` if no usable Delta exists.

There is no database required – the repository can be file-based
(e.g. `FileSystemShiftTrainingResultRepository`).

## 4. Adaptive CLI wiring

The `adaptive` CLI command in `EmbeddingShift.ConsoleEval` is wired to:

1. Resolve the results root for the "insurance" domain.
2. Construct a `FileSystemShiftTrainingResultRepository`.
3. Create a `TrainingBackedShiftGenerator` for `"mini-insurance-posneg"`.
4. Plug it into `ShiftEvaluationService` and `AdaptiveWorkflow`.

In short:

> Pos/Neg training → ShiftTrainingResult (Delta vector)  
> → TrainingBackedShiftGenerator → AdaptiveWorkflow → runtime shift selection.

This is the core bridge between the **statistics layer** and the
**adaptive shift selection** in the Mini-Insurance demo.