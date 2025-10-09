using System;
using System.Collections.Generic;
using System.Diagnostics;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Evaluators
{
    /// <summary>
    /// Orchestrates one or more <see cref="IShiftEvaluator"/> instances over a dataset,
    /// logs scores and timing via <see cref="IRunLogger"/>, and optionally compares
    /// a given shift against a NoShift baseline.
    /// </summary>
    /// <remarks>
    /// Complements <see cref="EmbeddingShift.Adaptive.ShiftEvaluationService"/>:
    /// <list type="bullet">
    /// <item><description><see cref="EmbeddingShift.Adaptive.ShiftEvaluationService"/> = local, selects best shift from a generator</description></item>
    /// <item><description><see cref="EvaluationRunner"/> = global, benchmarks shifts on datasets</description></item>
    /// </list>
    /// </remarks>
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

        private static double EvaluateAverage(
          IShiftEvaluator evaluator,
          IShift shift,
          IReadOnlyList<ReadOnlyMemory<float>> queries,
          IReadOnlyList<ReadOnlyMemory<float>> references)
        {
            double sum = 0;
            for (int i = 0; i < queries.Count; i++)
            {
                var res = evaluator.Evaluate(shift, queries[i].Span, references);
                sum += res.Score;
            }
            return sum / queries.Count;
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
            if (string.IsNullOrWhiteSpace(datasetName))
                datasetName = "dataset";

            var runId = _logger.StartRun("evaluation", datasetName);
            var overall = Stopwatch.StartNew();

            foreach (var evaluator in _evaluators)
            {
                var sw = Stopwatch.StartNew();
                var avg = EvaluateAverage(evaluator, shift, queries, references);
                sw.Stop();

                // metric: evaluator average score
                _logger.LogMetric(runId, evaluator.GetType().Name, avg);
                // metric: evaluator duration (ms)
                _logger.LogMetric(runId, $"{evaluator.GetType().Name}.duration_ms", sw.Elapsed.TotalMilliseconds);
            }

            overall.Stop();
            _logger.LogMetric(runId, "evaluation.duration_ms", overall.Elapsed.TotalMilliseconds);

            _logger.CompleteRun(runId,
                $"./results/{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid()}");
        }

        /// <summary>
        /// Runs evaluation for the provided shift and compares it against a NoShift baseline.
        /// Logs absolute scores plus delta = (shift - baseline).
        /// </summary>
        public void RunEvaluationWithBaseline(
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
            if (string.IsNullOrWhiteSpace(datasetName))
                datasetName = "dataset";

            var runId = _logger.StartRun("evaluation+baseline", datasetName);

            var baseline = new EmbeddingShift.Core.Shifts.NoShiftIngestBased();

            foreach (var evaluator in _evaluators)
            {
                var avgShift = EvaluateAverage(evaluator, shift, queries, references);
                var avgBase = EvaluateAverage(evaluator, baseline, queries, references);
                var delta = avgShift - avgBase;

                var name = evaluator.GetType().Name;
                _logger.LogMetric(runId, $"{name}.baseline", avgBase);
                _logger.LogMetric(runId, $"{name}.shift", avgShift);
                _logger.LogMetric(runId, $"{name}.delta", delta);
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
