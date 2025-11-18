using System;

namespace EmbeddingShift.Core.Stats
{
    /// <summary>
    /// Minimal in-memory implementation of IStatsCollector.
    /// All methods are no-op; this is only meant for tests and simple runs.
    /// </summary>
    public sealed class InMemoryStatsCollector : IStatsCollector
    {
        private sealed class NoopScope : IDisposable
        {
            public void Dispose()
            {
                // no-op
            }
        }

        public Guid RunId { get; private set; } = Guid.NewGuid();

        public void StartRun(string name, string? description = null)
        {
            // Create a fresh run id, but otherwise ignore for now
            RunId = Guid.NewGuid();
        }

        public void EndRun(string? status = null)
        {
            // no-op
        }

        public IDisposable TrackStep(string name, string? details = null)
        {
            // In a fuller implementation we could track nested steps here.
            return new NoopScope();
        }

        public void RecordExternal(string name, int successCount, int failureCount, string? details = null)
        {
            // no-op
        }

        public void RecordMetric(string name, double value, string? details = null)
        {
            // no-op
        }

        public void RecordError(string name, Exception exception, string? details = null)
        {
            // no-op
        }
    }
}
