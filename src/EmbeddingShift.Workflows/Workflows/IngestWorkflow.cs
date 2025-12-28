using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// Orchestrates a simple ingest flow:
    /// - Parse lines (file or folder) via IIngestor
    /// - Embed each line via IEmbeddingProvider
    /// - Persist each embedding via IVectorStore using the given dataset as 'space'
    /// </summary>
    public sealed class IngestWorkflow
    {
        private readonly IIngestor _ingestor;
        private readonly IEmbeddingProvider _provider;
        private readonly IVectorStore _store;

        public IngestWorkflow(IIngestor ingestor, IEmbeddingProvider provider, IVectorStore store)
        {
            _ingestor = ingestor;
            _provider = provider;
            _store = store;
        }

        /// <summary>
        /// Runs ingest. Each parsed line becomes one embedding saved under the given dataset 'space'.
        /// </summary>
        public async Task RunAsync(string filePath, string dataset)
        {
            if (string.IsNullOrWhiteSpace(dataset))
                dataset = "default";

            foreach (var (text, order) in _ingestor.Parse(filePath))
            {
                // Create embedding
                var vec = await _provider.GetEmbeddingAsync(text);

                // Persist embedding under the dataset as 'space'
                // Persist embedding under the dataset as 'space'
                // Use a deterministic ID so ingest is reproducible and idempotent.
                var id = CreateStableId(dataset, order, text);
                await _store.SaveEmbeddingAsync(
                    id: id,
                    vector: vec.ToArray(),
                    space: dataset,               // <<< IMPORTANT: use dataset here
                    provider: _provider.Name,
                    dimensions: vec.Length
                );
            }
        }
        private static Guid CreateStableId(string space, int order, string text)
        {
            // Deterministic RFC4122-ish GUID (version 5 style) based on (space, order, text).
            // This is intentionally stable across runs so re-ingest overwrites the same files.
            var payload = $"{space}\n{order}\n{text}";
            var bytes = System.Text.Encoding.UTF8.GetBytes(payload);

            var hash = System.Security.Cryptography.SHA256.HashData(bytes);

            Span<byte> guidBytes = stackalloc byte[16];
            hash.AsSpan(0, 16).CopyTo(guidBytes);

            // Set version to 5 (0101) and variant to RFC 4122 (10xx).
            guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
            guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

            return new Guid(guidBytes);
        }

    }
}
