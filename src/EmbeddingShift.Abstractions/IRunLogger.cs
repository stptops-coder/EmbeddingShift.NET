namespace EmbeddingShift.Abstractions
{
    /// <summary>
    /// Provides structured logging of workflow runs (ingest, eval, training).
    /// Stores metadata, metrics, and file paths for reproducibility.
    /// </summary>
    public interface IRunLogger
    {
        /// <summary>
        /// Starts a new run and returns its ID.
        /// </summary>
        Guid StartRun(string kind, string dataset);

        /// <summary>
        /// Records a metric (score) for the given run.
        /// </summary>
        void LogMetric(Guid runId, string metric, double score);

        /// <summary>
        /// Completes the run and stores the results path.
        /// </summary>
        void CompleteRun(Guid runId, string resultsPath);
    }
}
