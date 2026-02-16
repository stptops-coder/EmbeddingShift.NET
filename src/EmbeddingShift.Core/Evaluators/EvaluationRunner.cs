using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Infrastructure;

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
        /// Logs results with <see cref="IRunLogger"/>.
        /// </summary>
        public void RunEvaluation(
            IShift shift,
            IReadOnlyList<ReadOnlyMemory<float>> queries,
            IReadOnlyList<ReadOnlyMemory<float>> references,
            string datasetName)
        {
            _ = RunEvaluationSummary(shift, queries, references, datasetName);
        }

        /// <summary>
        /// Runs evaluation and returns a structured summary (UI/automation friendly).
        /// </summary>
        public EvaluationRunSummary RunEvaluationSummary(
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

            var startedAtUtc = DateTime.UtcNow;
            var runId = _logger.StartRun("evaluation", datasetName);

            var metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            var overall = Stopwatch.StartNew();

            foreach (var evaluator in _evaluators)
            {
                var sw = Stopwatch.StartNew();
                var avg = EvaluateAverage(evaluator, shift, queries, references);
                sw.Stop();

                var name = evaluator.GetType().Name;

                // metric: evaluator average score
                _logger.LogMetric(runId, name, avg);
                metrics[name] = avg;

                // metric: evaluator duration (ms)
                var durKey = $"{name}.duration_ms";
                var dur = sw.Elapsed.TotalMilliseconds;
                _logger.LogMetric(runId, durKey, dur);
                metrics[durKey] = dur;
            }

            overall.Stop();
            var overallKey = "evaluation.duration_ms";
            var overallMs = overall.Elapsed.TotalMilliseconds;
            _logger.LogMetric(runId, overallKey, overallMs);
            metrics[overallKey] = overallMs;

            var resultsPath = CreateResultsDirectory("evaluation", runId);
            _logger.CompleteRun(runId, resultsPath);

            var completedAtUtc = DateTime.UtcNow;

            return new EvaluationRunSummary(
                RunId: runId,
                Kind: "evaluation",
                Dataset: datasetName,
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: completedAtUtc,
                ResultsPath: resultsPath,
                Metrics: metrics);
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
            _ = RunEvaluationWithBaselineSummary(shift, queries, references, datasetName);
        }

        /// <summary>
        /// Runs evaluation with a NoShift baseline and returns a structured summary.
        /// </summary>
        public EvaluationRunSummary RunEvaluationWithBaselineSummary(
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

            var startedAtUtc = DateTime.UtcNow;
            var runId = _logger.StartRun("evaluation+baseline", datasetName);

            var metrics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            var baseline = new EmbeddingShift.Core.Shifts.NoShiftIngestBased();

            foreach (var evaluator in _evaluators)
            {
                var avgShift = EvaluateAverage(evaluator, shift, queries, references);
                var avgBase = EvaluateAverage(evaluator, baseline, queries, references);
                var delta = avgShift - avgBase;

                var name = evaluator.GetType().Name;

                var kBase = $"{name}.baseline";
                var kShift = $"{name}.shift";
                var kDelta = $"{name}.delta";

                _logger.LogMetric(runId, kBase, avgBase);
                _logger.LogMetric(runId, kShift, avgShift);
                _logger.LogMetric(runId, kDelta, delta);

                metrics[kBase] = avgBase;
                metrics[kShift] = avgShift;
                metrics[kDelta] = delta;
            }

            var resultsPath = CreateResultsDirectory("evaluation+baseline", runId);
            _logger.CompleteRun(runId, resultsPath);

            var completedAtUtc = DateTime.UtcNow;

            return new EvaluationRunSummary(
                RunId: runId,
                Kind: "evaluation+baseline",
                Dataset: datasetName,
                StartedAtUtc: startedAtUtc,
                CompletedAtUtc: completedAtUtc,
                ResultsPath: resultsPath,
                Metrics: metrics);
        }

        private static string CreateResultsDirectory(string kind, Guid runId)
        {
            var domainKeyRaw = Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_RESULTS_DOMAIN");
            var domainKey = string.IsNullOrWhiteSpace(domainKeyRaw)
                ? "insurance"
                : SanitizePathPart(domainKeyRaw);

            var root = Path.Combine(DirectoryLayout.ResolveResultsRoot(domainKey), "runs");
            var safeKind = SanitizePathPart(kind);
            var dirName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{safeKind}_{runId:N}";
            var path = Path.Combine(root, dirName);
            Directory.CreateDirectory(path);
            return path;
        }

        private static string SanitizePathPart(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "run";

            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';

            var sanitized = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "run" : sanitized;
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
