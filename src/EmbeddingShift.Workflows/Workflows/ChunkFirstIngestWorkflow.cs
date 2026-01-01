using System.Text;
using System.Text.Json;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Infrastructure;
using EmbeddingShift.Preprocessing;

namespace EmbeddingShift.Workflows
{
    public sealed record ChunkFirstIngestOptions(
        string InputRoot,
        int ChunkSize,
        int ChunkOverlap,
        bool Recursive = true);

    public sealed record ChunkFirstIngestManifestSummary(
        string Id,
        DateTime StartedUtc,
        DateTime FinishedUtc,
        string Space,
        string Provider,
        int Dimensions,
        string InputRoot,
        ChunkFirstIngestPreprocessing Preprocessing,
        long TotalDocuments,
        long TotalChunks,
        string ChunkIndexFileName);

    public sealed record ChunkFirstIngestPreprocessing(
        string Loader,
        string Transformer,
        string Chunker,
        int ChunkSize,
        int ChunkOverlap);

    public sealed record ChunkFirstIngestChunkIndexRecord(
        string Id,
        string Doc,
        int ChunkIndex,
        int CharCount,
        string TextSha256);

    /// <summary>
    /// Canonical "chunk-first" ingest:
    /// - Enumerate input files (deterministic order)
    /// - PreprocessPipeline: load -> transform -> chunk
    /// - Embed each chunk
    /// - Persist embeddings into FileStore under the provided logical space (e.g. "DemoDataset:refs")
    /// - Write a manifest summary + a chunk index JSONL for traceability (doc->chunk->embedding id)
    /// </summary>
    public sealed class ChunkFirstIngestWorkflow
    {
        private readonly PreprocessPipeline _pipeline;
        private readonly IEmbeddingProvider _provider;
        private readonly IVectorStore _store;

        private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private static readonly JsonSerializerOptions Jl = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

        public ChunkFirstIngestWorkflow(
            PreprocessPipeline pipeline,
            IEmbeddingProvider provider,
            IVectorStore store)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public async Task<string> RunAsync(string inputPath, string space, ChunkFirstIngestOptions options)
        {
            if (string.IsNullOrWhiteSpace(inputPath)) throw new ArgumentException("Input path is required.", nameof(inputPath));
            if (string.IsNullOrWhiteSpace(space)) throw new ArgumentException("Space is required.", nameof(space));

            var startedUtc = DateTime.UtcNow;
            var manifestId = Guid.NewGuid();

            var inputFull = Path.GetFullPath(inputPath);
            var isDir = Directory.Exists(inputFull);
            var baseRoot = NormalizeBaseRoot(inputFull, isDir, options.InputRoot);

            var files = EnumerateFilesDeterministic(inputFull, isDir, options.Recursive);

            var manifestsRoot = DirectoryLayout.ResolveDataRoot("manifests");
            var manifestSpaceDir = Path.Combine(manifestsRoot, SpaceToPath(space));
            Directory.CreateDirectory(manifestSpaceDir);

            var chunkIndexFileName = $"chunks_{manifestId:N}.jsonl";
            var chunkIndexPath = Path.Combine(manifestSpaceDir, chunkIndexFileName);

            long totalDocs = 0;
            long totalChunks = 0;
            int dims = 0;

            await using var chunkIndexStream = new FileStream(chunkIndexPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await using var chunkIndexWriter = new StreamWriter(chunkIndexStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!IsSupportedTextExtension(ext))
                    continue;

                totalDocs++;

                var relDoc = Path.GetRelativePath(baseRoot, file);

                var chunkIndex = 0;
                foreach (var (chunk, _) in _pipeline.Run(file))
                {
                    if (string.IsNullOrWhiteSpace(chunk))
                    {
                        chunkIndex++;
                        continue;
                    }

                    var embedding = await _provider.GetEmbeddingAsync(chunk);
                    dims = embedding.Length;

                    var id = CreateStableId(space, relDoc, chunkIndex, chunk);

                    await _store.SaveEmbeddingAsync(
                        id: id,
                        vector: embedding,
                        space: space,
                        provider: _provider.Name,
                        dimensions: dims);

                    var rec = new ChunkFirstIngestChunkIndexRecord(
                        Id: id.ToString("N"),
                        Doc: relDoc.Replace('\\', '/'),
                        ChunkIndex: chunkIndex,
                        CharCount: chunk.Length,
                        TextSha256: Sha256Hex(chunk));

                    await chunkIndexWriter.WriteLineAsync(JsonSerializer.Serialize(rec, Jl));

                    totalChunks++;
                    chunkIndex++;
                }
            }

            await chunkIndexWriter.FlushAsync();

            var finishedUtc = DateTime.UtcNow;

            var summary = new ChunkFirstIngestManifestSummary(
                Id: manifestId.ToString("N"),
                StartedUtc: startedUtc,
                FinishedUtc: finishedUtc,
                Space: space.Trim(),
                Provider: _provider.Name,
                Dimensions: dims,
                InputRoot: baseRoot.Replace('\\', '/'),
                Preprocessing: new ChunkFirstIngestPreprocessing(
                    Loader: "TxtLoader(.txt|.log|.md)",
                    Transformer: "Normalizer",
                    Chunker: "FixedChunker",
                    ChunkSize: options.ChunkSize,
                    ChunkOverlap: options.ChunkOverlap),
                TotalDocuments: totalDocs,
                TotalChunks: totalChunks,
                ChunkIndexFileName: chunkIndexFileName);

            var summaryPath = Path.Combine(manifestSpaceDir, $"manifest_{manifestId:N}.json");
            await File.WriteAllTextAsync(summaryPath, JsonSerializer.Serialize(summary, J), new UTF8Encoding(false));
            // Convenience pointer for callers: always keep an overwritable 'latest' manifest alongside versioned ones.
            // This keeps the ingest stream deterministic for consumers while still retaining history.
            var latestPath = Path.Combine(manifestSpaceDir, "manifest_latest.json");
            await File.WriteAllTextAsync(latestPath, JsonSerializer.Serialize(summary, J), new UTF8Encoding(false));
    
            return summaryPath;
        }

        private static string NormalizeBaseRoot(string inputFull, bool isDir, string optionsRoot)
        {
            if (!string.IsNullOrWhiteSpace(optionsRoot))
            {
                var opt = Path.GetFullPath(optionsRoot);
                if (Directory.Exists(opt)) return opt;
            }

            if (isDir) return inputFull;

            var dir = Path.GetDirectoryName(inputFull);
            return string.IsNullOrWhiteSpace(dir) ? Directory.GetCurrentDirectory() : dir;
        }

        private static IEnumerable<string> EnumerateFilesDeterministic(string inputFull, bool isDir, bool recursive)
        {
            if (!isDir)
            {
                if (File.Exists(inputFull)) yield return inputFull;
                yield break;
            }

            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var f in Directory.EnumerateFiles(inputFull, "*.*", option)
                                      .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                yield return f;
            }
        }

        private static bool IsSupportedTextExtension(string ext)
            => ext is ".txt" or ".log" or ".md";

        private static Guid CreateStableId(string space, string doc, int chunkIndex, string chunkText)
        {
            var payload = $"{space}\n{doc}\n{chunkIndex}\n{chunkText}";
            var bytes = Encoding.UTF8.GetBytes(payload);

            var hash = System.Security.Cryptography.SHA256.HashData(bytes);

            Span<byte> guidBytes = stackalloc byte[16];
            hash.AsSpan(0, 16).CopyTo(guidBytes);

            // version 5 + RFC4122 variant
            guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
            guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

            return new Guid(guidBytes);
        }

        private static string Sha256Hex(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            var hash = System.Security.Cryptography.SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string SpaceToPath(string space)
            => SpacePath.ToRelativePath(space);

    }
}
