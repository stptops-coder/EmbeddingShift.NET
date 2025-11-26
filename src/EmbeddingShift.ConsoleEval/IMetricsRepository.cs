using System;

namespace EmbeddingShift.ConsoleEval
{
    /// <summary>
    /// Abstraction for persisting metrics artifacts. First use case:
    /// mini-insurance First/Delta comparisons and aggregates.
    /// Later this can be implemented with a database-backed store.
    /// </summary>
    public interface IMetricsRepository
    {
        /// <summary>
        /// Persists a single mini-insurance First/Delta comparison
        /// and returns the directory where the artifacts were written.
        /// </summary>
        string SaveMiniInsuranceFirstDeltaComparison(
            string baseDirectory,
            MiniInsuranceFirstDeltaComparison comparison);

        /// <summary>
        /// Persists aggregated metrics for mini-insurance First/Delta runs
        /// and returns the directory where the artifacts were written.
        /// </summary>
        string SaveMiniInsuranceFirstDeltaAggregate(
            string baseDirectory,
            MiniInsuranceFirstDeltaAggregate aggregate);
    }
}
