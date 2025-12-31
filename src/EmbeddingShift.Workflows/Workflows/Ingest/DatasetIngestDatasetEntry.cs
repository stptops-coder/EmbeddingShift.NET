using System;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Workflows.Ingest
{
    public sealed record DatasetIngestDatasetRequest(
        string Dataset,
        string RefsPath,
        string QueriesPath,
        DatasetIngestMode RefsMode = DatasetIngestMode.ChunkFirst,
        int ChunkSize = 1000,
        int ChunkOverlap = 100,
        bool Recursive = true);

    public sealed record DatasetIngestDatasetResult(
        DatasetIngestResult RefsIngest,
        DatasetIngestResult QueriesIngest);

    /// <summary>
    /// Canonical ingestion entrypoint: ingests Refs + Queries for a dataset in one step.
    /// - Refs: default ChunkFirst (manifest_latest + versioned manifest)
    /// - Queries: Plain (auto-detects queries.json via existing DatasetIngestEntry logic)
    /// </summary>
    public sealed class DatasetIngestDatasetEntry
    {
        private readonly DatasetIngestEntry _ingestEntry;

        public DatasetIngestDatasetEntry(DatasetIngestEntry ingestEntry)
        {
            _ingestEntry = ingestEntry ?? throw new ArgumentNullException(nameof(ingestEntry));
        }

        public async Task<DatasetIngestDatasetResult> RunAsync(
            DatasetIngestDatasetRequest request,
            IIngestor textLineIngestor,
            IIngestor queriesJsonIngestor,
            CancellationToken ct = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (textLineIngestor is null) throw new ArgumentNullException(nameof(textLineIngestor));
            if (queriesJsonIngestor is null) throw new ArgumentNullException(nameof(queriesJsonIngestor));

            ct.ThrowIfCancellationRequested();

            var refsRes = await _ingestEntry.RunAsync(
                new DatasetIngestRequest(
                    Dataset: request.Dataset,
                    Role: "refs",
                    InputPath: request.RefsPath,
                    Mode: request.RefsMode,
                    ChunkSize: request.ChunkSize,
                    ChunkOverlap: request.ChunkOverlap,
                    Recursive: request.Recursive),
                textLineIngestor: textLineIngestor);

            ct.ThrowIfCancellationRequested();

            var queriesRes = await _ingestEntry.RunAsync(
                new DatasetIngestRequest(
                    Dataset: request.Dataset,
                    Role: "queries",
                    InputPath: request.QueriesPath,
                    Mode: DatasetIngestMode.Plain),
                textLineIngestor: textLineIngestor,
                queriesJsonIngestor: queriesJsonIngestor);

            return new DatasetIngestDatasetResult(refsRes, queriesRes);
        }
    }
}
