# Evaluators in EmbeddingShift

This folder contains evaluators used to measure the quality of shift strategies.  
Evaluators implement `IShiftEvaluator` and are located under **`EmbeddingShift.Core.Evaluators`**.

## When to use which evaluator?

- **CosineSimilarityEvaluator**
  - Default choice, robust and simple.
  - Use when you just want to measure general alignment between shifted queries and reference embeddings.

- **MarginEvaluator**
  - Measures stability by comparing Top-1 vs. Top-2 scores.
  - Use when you want a confident winner (clear separation at the top).

- **MrrEvaluator**
  - Mean Reciprocal Rank, assumes exactly one correct answer.
  - Use in QA-style setups where only one reference is considered the gold answer.

- **NdcgEvaluator**
  - Normalized Discounted Cumulative Gain, supports multiple relevant answers.
  - Use when ranking quality matters (e.g., top-K retrieval with more than one relevant item).

## Usage

Evaluators are typically not called directly.  
They are passed into the **`ShiftEvaluationService`** (in `EmbeddingShift.Adaptive`)  
which runs candidate shifts through the evaluators and returns an `EvaluationReport`.

Example:

```csharp
var evals = new IShiftEvaluator[]
{
    new CosineSimilarityEvaluator(),
    new MarginEvaluator(),
    new MrrEvaluator(0),
    new NdcgEvaluator(new[]{0, 2}, k:5)
};

var service = new ShiftEvaluationService(generator, evals);
var report = service.Evaluate(pairs);
```
