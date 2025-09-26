using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// Ingest workflow: parse, chunk, embed, and store.
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

        public async Task RunAsync(string filePath, string dataset)
        {
            var chunks = _ingestor.Parse(filePath);
            foreach (var chunk in chunks)
            {
                var embedding = await _provider.GetEmbeddingAsync(chunk.Text);
                await _store.SaveEmbeddingAsync(Guid.NewGuid(), embedding, "base", _provider.Name, embedding.Length);
            }
        }
    }
}
