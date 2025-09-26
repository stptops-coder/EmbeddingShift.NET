// EmbeddingShift.Abstractions/IVectorStore.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EmbeddingShift.Abstractions
{
    /// <summary>
    /// Abstraction for storing and retrieving embeddings, shifts, and run metadata.
    /// Allows switching between SQLite, in-memory, or other persistence backends.
    /// </summary>
    public interface IVectorStore
    {
        Task SaveEmbeddingAsync(Guid id, float[] vector, string space, string provider, int dimensions);
        Task<float[]> LoadEmbeddingAsync(Guid id);

        Task SaveShiftAsync(Guid id, string type, string parametersJson);
        Task<IEnumerable<(Guid id, string type, string parametersJson)>> LoadShiftsAsync();

        Task SaveRunAsync(Guid runId, string kind, string dataset, DateTime startedAt, DateTime completedAt, string resultsPath);
    }
}
