using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// Deferred persistence boundary for replaying storage-backed vector artifacts.
    /// The current verification baseline persists file-based run artifacts; database replay is outside the active gate.
    /// </summary>
    public sealed class PersistenceWorkflow
    {
        private readonly IVectorStore _store;

        public PersistenceWorkflow(IVectorStore store)
        {
            _store = store;
        }

        public async Task ReplayAsync(string sqlFilePath)
        {
            var sql = await File.ReadAllTextAsync(sqlFilePath);
            // Database replay is intentionally outside the current file-based verification baseline.
            _ = sql;
            _ = _store;
        }
    }
}
