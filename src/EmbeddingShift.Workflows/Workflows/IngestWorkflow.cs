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
                var id = Guid.NewGuid();
                await _store.SaveEmbeddingAsync(
                    id: id,
                    vector: vec.ToArray(),
                    space: dataset,               // <<< IMPORTANT: use dataset here
                    provider: _provider.Name,
                    dimensions: vec.Length
                );
            }
        }
    }
}
