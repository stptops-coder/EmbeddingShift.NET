using EmbeddingShift.Abstractions;
using EmbeddingShift.Workflows.Eval;
using EmbeddingShift.Workflows.Ingest;

namespace EmbeddingShift.Workflows.Run
{
    public sealed record DatasetRunRequest(
        string Dataset,
        string RefsPath,
        string QueriesPath,
        DatasetIngestMode RefsMode = DatasetIngestMode.ChunkFirst,
        int ChunkSize = 1000,
        int ChunkOverlap = 100,
        bool Recursive = true,
        bool EvalUseSim = false,
        bool EvalUseBaseline = false);

    public sealed record DatasetRunResult(
        DatasetIngestResult RefsIngest,
        DatasetIngestResult QueriesIngest,
        DatasetEvalResult EvalResult);

    /// <summary>
    /// Canonical orchestrator for the common flow: ingest (refs + queries) then eval.
    /// CLI parses arguments, then calls this entry. UI calls the same entry.
    /// </summary>
    public sealed class DatasetRunEntry
    {
        private readonly DatasetIngestDatasetEntry _ingestDatasetEntry;
        private readonly DatasetEvalEntry _evalEntry;

        public DatasetRunEntry(DatasetIngestDatasetEntry ingestDatasetEntry, DatasetEvalEntry evalEntry)
        {
            _ingestDatasetEntry = ingestDatasetEntry ?? throw new ArgumentNullException(nameof(ingestDatasetEntry));
            _evalEntry = evalEntry ?? throw new ArgumentNullException(nameof(evalEntry));
        }

        public async Task<DatasetRunResult> RunAsync(
            IShift shift,
            DatasetRunRequest request,
            IIngestor textLineIngestor,
            IIngestor queriesJsonIngestor,
            CancellationToken ct = default)
        {
            if (shift is null) throw new ArgumentNullException(nameof(shift));
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (textLineIngestor is null) throw new ArgumentNullException(nameof(textLineIngestor));
            if (queriesJsonIngestor is null) throw new ArgumentNullException(nameof(queriesJsonIngestor));

            var dataset = string.IsNullOrWhiteSpace(request.Dataset) ? "DemoDataset" : request.Dataset.Trim();

            // 1) Ingest refs + queries (canonical path)
            var ingestRes = await _ingestDatasetEntry.RunAsync(
                new DatasetIngestDatasetRequest(
                    Dataset: dataset,
                    RefsPath: request.RefsPath,
                    QueriesPath: request.QueriesPath,
                    RefsMode: request.RefsMode,
                    ChunkSize: request.ChunkSize,
                    ChunkOverlap: request.ChunkOverlap,
                    Recursive: request.Recursive),
                textLineIngestor,
                queriesJsonIngestor,
                ct);

            // 2) Evaluate (persisted by default)
            var evalRes = await _evalEntry.RunAsync(
                shift,
                new DatasetEvalRequest(
                    Dataset: dataset,
                    UseSim: request.EvalUseSim,
                    UseBaseline: request.EvalUseBaseline),
                ct);

            return new DatasetRunResult(
                RefsIngest: ingestRes.RefsIngest,
                QueriesIngest: ingestRes.QueriesIngest,
                EvalResult: evalRes);
        }
    }
}
