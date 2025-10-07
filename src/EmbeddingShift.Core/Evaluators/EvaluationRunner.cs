using System;
using System.Collections.Generic;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Evaluators
{
    /// <summary>
    /// Global evaluation runner:
    /// Orchestrates evaluators across full query sets and logs results
    /// via IRunLogger.
    ///
    /// Complementary to Adaptive.ShiftEvaluationService:
    /// - ShiftEvaluationService = local, selects best shift from generator
    /// - EvaluationRunner = global, benchmarks shifts on datasets
    /// </summary>
    public sealed class EvaluationRunner
    {
        private readonly IReadOnlyList<IShiftEvaluator> _evaluators;
        private readonly IRunLogger _logger;

        public EvaluationRunner(IEnumerable<IShiftEvaluator> evaluators, IRunLogger logger)
        {
            if (evaluators is null) throw new ArgumentNullException(nameof(evaluators));
            _evaluators = new List<IShiftEvaluator>(evaluators);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Run all evaluators on the given shift and dataset.
        /// Logs results persistently with IRunLogger.
        /// </summary>
        public void RunEvaluation(
            IShift shift,
            IReadOnlyList<ReadOnlyMemory<float>> queries,
            IReadOnlyList<ReadOnlyMemory<float>> references,
            string datasetName)
        {
            if (shift == null) throw new ArgumentNullException(nameof(shift));
            if (queries == null || queries.Count == 0)
                throw new ArgumentException("Queries must not be empty", nameof(queries));
            if (references == null || references.Count == 0)
                throw new ArgumentException("References must not be empty", nameof(references));

            // TODO [Baseline]: If no baseline (NoShiftIngestBased) is provided, create one internally
            // and log Δ-values (shifted vs. baseline) alongside absolute metrics.

            var runId = _logger.StartRun("evaluation", datasetName);

            foreach (var evaluator in _evaluators)
            {
                double score = 0;
                foreach (var query in queries)
                {
                    var result = evaluator.Evaluate(shift, query.Span, references);
                    score += result.Score;
                }

                score /= queries.Count;
                _logger.LogMetric(runId, evaluator.GetType().Name, score);
            }

            _logger.CompleteRun(runId,
                $"./results/{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid()}");
        }

        /// <summary>
        /// Convenience factory with common default evaluators.
        /// Adjust as needed for your domain.
        /// </summary>
        public static EvaluationRunner WithDefaults(IRunLogger logger) =>
            new EvaluationRunner(new IShiftEvaluator[]
            {
                new CosineSimilarityEvaluator(),
                new MarginEvaluator(),
                new NdcgEvaluator(new []{0}, 10),
                new MrrEvaluator()
            }, logger);
    }
}
