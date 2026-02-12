using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Infrastructure;
using EmbeddingShift.Preprocessing;
using EmbeddingShift.Preprocessing.Chunking;
using EmbeddingShift.Preprocessing.Loading;
using EmbeddingShift.Preprocessing.Transform;
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace EmbeddingShift.Workflows.Ingest
{
    public enum DatasetIngestMode
    {
        Plain = 0,
        ChunkFirst = 1
    }

    public sealed record DatasetIngestRequest(
        string Dataset,
        string Role,
        string InputPath,
        DatasetIngestMode Mode = DatasetIngestMode.Plain,
        int ChunkSize = 1000,
        int ChunkOverlap = 100,
        bool Recursive = true);

    public sealed record DatasetIngestResult(
        string Space,
        DatasetIngestMode Mode,
        bool UsedJson,
        string? ManifestPath);

    /// <summary>
    /// Canonical, domain-neutral ingest entrypoint:
    /// - Plain ingest: text lines (.txt) or (for queries) queries.json
    /// - Chunk-first ingest: load documents, normalize, chunk, embed chunks, persist, write manifest/index
    ///
    /// This is intended to be used by CLI and future UI equally.
    /// </summary>
    public sealed class DatasetIngestEntry
    {
        private readonly IEmbeddingProvider _provider;
        private readonly IVectorStore _store;

        private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private sealed record PlainIngestManifestSummary(
            string Space,
            string Dataset,
            string Role,
            string Mode,
            bool UsedJson,
            string Provider,
            string InputPath,
            DateTime CreatedUtc);

        private static async Task<string> WritePlainManifestAsync(
            string space,
            DatasetIngestRequest request,
            bool usedJson,
            string providerName,
            DateTime createdUtc,
            CancellationToken ct)
        {
            var manifestsRoot = DirectoryLayout.ResolveDataRoot("manifests");
            var manifestSpaceDir = Path.Combine(manifestsRoot, SpacePath.ToRelativePath(space));
            Directory.CreateDirectory(manifestSpaceDir);

            var summary = new PlainIngestManifestSummary(
                Space: space,
                Dataset: request.Dataset,
                Role: request.Role,
                Mode: request.Mode.ToString(),
                UsedJson: usedJson,
                Provider: providerName,
                InputPath: Path.GetFullPath(request.InputPath),
                CreatedUtc: createdUtc);

            var id = Guid.NewGuid().ToString("N");
            var summaryPath = Path.Combine(manifestSpaceDir, $"manifest_{id}.json");

            var json = JsonSerializer.Serialize(summary, J);
            await File.WriteAllTextAsync(summaryPath, json, new UTF8Encoding(false), ct);

            var latestPath = Path.Combine(manifestSpaceDir, "manifest_latest.json");
            await File.WriteAllTextAsync(latestPath, json, new UTF8Encoding(false), ct);

            return summaryPath;
        }

        public DatasetIngestEntry(IEmbeddingProvider provider, IVectorStore store)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public async Task<DatasetIngestResult> RunAsync(
            DatasetIngestRequest request,
            IIngestor textLineIngestor,
            IIngestor? queriesJsonIngestor = null,
            CancellationToken ct = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (textLineIngestor is null) throw new ArgumentNullException(nameof(textLineIngestor));

            var space = $"{request.Dataset}:{request.Role}".Trim();

            // Current persistence is file-based; clear previous embeddings deterministically for this space.
            EmbeddingSpaceCleaner.ClearEmbeddingsForSpace(space);

            if (request.Mode == DatasetIngestMode.ChunkFirst)
            {
                var pipeline = new PreprocessPipeline(
                    loaders: new IDocumentLoader[] { new TxtLoader() },
                    transformer: new Normalizer(),
                    chunker: new FixedChunker(size: request.ChunkSize, overlap: request.ChunkOverlap));

                var wf = new ChunkFirstIngestWorkflow(pipeline, _provider, _store);

                var manifest = await wf.RunAsync(
                    inputPath: request.InputPath,
                    space: space,
                    options: new ChunkFirstIngestOptions(
                        InputRoot: request.InputPath,
                        ChunkSize: request.ChunkSize,
                        ChunkOverlap: request.ChunkOverlap,
                        Recursive: request.Recursive));

                await EmbeddingSpaceStateStore.TryWriteAsync(
                    embeddingsRoot: DirectoryLayout.ResolveDataRoot("embeddings"),
                    state: new EmbeddingSpaceState(
                        Space: space,
                        Mode: request.Mode.ToString(),
                        UsedJson: false,
                        Provider: _provider.Name,
                        CreatedUtc: DateTime.UtcNow,
                        ChunkFirstManifestPath: manifest));

                return new DatasetIngestResult(
                    Space: space,
                    Mode: request.Mode,
                    UsedJson: false,
                    ManifestPath: manifest);
            }

            var usedJson = false;
            var ingestor = textLineIngestor;

            if (string.Equals(request.Role, "queries", StringComparison.OrdinalIgnoreCase) &&
                queriesJsonIngestor is not null &&
                ShouldUseQueriesJson(request.InputPath))
            {
                ingestor = queriesJsonIngestor;
                usedJson = true;
            }

            var wfPlain = new IngestWorkflow(ingestor, _provider, _store);
            await wfPlain.RunAsync(request.InputPath, space);

            var createdUtc = DateTime.UtcNow;
            var plainManifestPath = await WritePlainManifestAsync(
                space: space,
                request: request,
                usedJson: usedJson,
                providerName: _provider.Name,
                createdUtc: createdUtc,
                ct: ct);

            await EmbeddingSpaceStateStore.TryWriteAsync(
                embeddingsRoot: DirectoryLayout.ResolveDataRoot("embeddings"),
                state: new EmbeddingSpaceState(
                    Space: space,
                    Mode: request.Mode.ToString(),
                    UsedJson: usedJson,
                    Provider: _provider.Name,
                    CreatedUtc: createdUtc,
                    ChunkFirstManifestPath: null));

            return new DatasetIngestResult(
                Space: space,
                Mode: request.Mode,
                UsedJson: usedJson,
                ManifestPath: plainManifestPath);
        }

        private static bool ShouldUseQueriesJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (Directory.Exists(input))
            {
                var candidate = Path.Combine(input, "queries.json");
                return File.Exists(candidate);
            }

            if (!File.Exists(input))
                return false;

            return string.Equals(
                Path.GetFileName(input),
                "queries.json",
                StringComparison.OrdinalIgnoreCase);
        }

        private static class EmbeddingSpaceCleaner
        {
            public static void ClearEmbeddingsForSpace(string space)
            {
                var embeddingsRoot = DirectoryLayout.ResolveDataRoot("embeddings");
                var dir = Path.Combine(embeddingsRoot, SpaceToPath(space));

                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }

            private static string SpaceToPath(string space)
                => SpacePath.ToRelativePath(space);
        }
    }
}
