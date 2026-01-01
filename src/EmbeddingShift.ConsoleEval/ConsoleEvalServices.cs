using EmbeddingShift.Abstractions;
using EmbeddingShift.Workflows.Eval;
using EmbeddingShift.Workflows.Ingest;
using EmbeddingShift.Workflows.Run;

namespace EmbeddingShift.ConsoleEval;

/// <summary>
/// Bundles the console-harness entrypoints and shared components, so the CLI dispatcher
/// can stay decoupled from the composition root.
/// </summary>
public sealed record ConsoleEvalServices(
    ShiftMethod Method,
    DatasetIngestEntry IngestEntry,
    DatasetIngestDatasetEntry IngestDatasetEntry,
    DatasetEvalEntry EvalEntry,
    DatasetRunEntry RunEntry,
    IIngestor TxtLineIngestor,
    IIngestor QueriesJsonIngestor);
