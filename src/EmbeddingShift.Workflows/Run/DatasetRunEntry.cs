using EmbeddingShift.Abstractions;
using EmbeddingShift.Workflows.Run;
using EmbeddingShift.Workflows.Ingest;
using EmbeddingShift.Workflows.Eval;


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
        bool EvalUseSim = false);

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
        private readonly DatasetIngestEntry _ingestEntry;
        private readonly DatasetEvalEntry _evalEntry;

        public DatasetRunEntry(DatasetIngestEntry ingestEntry, DatasetEvalEntry evalEntry)
        {
            _ingestEntry = ingestEntry ?? throw new ArgumentNullException(nameof(ingestEntry));
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

            // 1) Ingest refs (plain or chunk-first)
            var refsIngest = await _ingestEntry.RunAsync(
                new DatasetIngestRequest(
                    Dataset: dataset,
                    Role: "refs",
                    InputPath: request.RefsPath,
                    Mode: request.RefsMode,
                    ChunkSize: request.ChunkSize,
                    ChunkOverlap: request.ChunkOverlap,
                    Recursive: request.Recursive),
                textLineIngestor: textLineIngestor);

            // 2) Ingest queries (plain; supports queries.json automatically)
            var queriesIngest = await _ingestEntry.RunAsync(
                new DatasetIngestRequest(
                    Dataset: dataset,
                    Role: "queries",
                    InputPath: request.QueriesPath,
                    Mode: DatasetIngestMode.Plain),
                textLineIngestor: textLineIngestor,
                queriesJsonIngestor: queriesJsonIngestor);

            // 3) Evaluate (persisted by default)
            var evalRes = await _evalEntry.RunAsync(
                shift,
                new DatasetEvalRequest(
                    Dataset: dataset,
                    UseSim: request.EvalUseSim),
                ct);

            return new DatasetRunResult(
                RefsIngest: refsIngest,
                QueriesIngest: queriesIngest,
                EvalResult: evalRes);
        }
    }
}
