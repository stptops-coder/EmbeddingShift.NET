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

Evaluators are used by the dataset-level evaluation flow to score retrieval results.
The standard path is the ConsoleEval run/eval pipeline, not a separate demo service.
