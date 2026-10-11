# Baseline Shift (NoShiftIngestBased)

`NoShiftIngestBased` is the identity transformation: it leaves the query vector
unchanged. Its scores provide a control for comparison with shifted queries.
The baseline uses the same embedding provider and reference set as the shifted run.

The verified local workflow uses simulated embeddings. This baseline does not,
by itself, demonstrate retrieval quality from a pretrained language model.

## Example

From the repository root:

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- run-smoke-demo --baseline
```

For an already ingested dataset:

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- eval MyDataset --baseline
```

The generic evaluators report cosine similarity, margin, nDCG and MRR, with
baseline/shift/delta values. Their default relevant reference is index zero;
this is a diagnostic setup, not per-query gold-label evaluation. See
[metric definitions](../../../Docs/CLI-Metrics.md) for that distinction.

Embeddings and ingest manifests are stored under `data/`; evaluation artifacts
are written under `results/`. `EMBEDDINGSHIFT_ROOT` can relocate both roots.
