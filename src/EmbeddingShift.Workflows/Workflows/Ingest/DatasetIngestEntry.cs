using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Infrastructure;
using EmbeddingShift.Preprocessing;
using EmbeddingShift.Preprocessing.Chunking;
using EmbeddingShift.Preprocessing.Loading;
using EmbeddingShift.Preprocessing.Transform;

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

        public DatasetIngestEntry(IEmbeddingProvider provider, IVectorStore store)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public async Task<DatasetIngestResult> RunAsync(
            DatasetIngestRequest request,
            IIngestor textLineIngestor,
            IIngestor? queriesJsonIngestor = null)
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
            await EmbeddingSpaceStateStore.TryWriteAsync(
    embeddingsRoot: DirectoryLayout.ResolveDataRoot("embeddings"),
    state: new EmbeddingSpaceState(
        Space: space,
        Mode: request.Mode.ToString(),
        UsedJson: usedJson,
        Provider: _provider.Name,
        CreatedUtc: DateTime.UtcNow,
        ChunkFirstManifestPath: null));

            return new DatasetIngestResult(
                Space: space,
                Mode: request.Mode,
                UsedJson: usedJson,
                ManifestPath: null);
        }

        private static bool ShouldUseQueriesJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var full = Path.GetFullPath(input);

            if (Directory.Exists(full))
                return File.Exists(Path.Combine(full, "queries.json"));

            if (File.Exists(full))
                return string.Equals(Path.GetExtension(full), ".json", StringComparison.OrdinalIgnoreCase);

            return false;
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
