using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Evaluators;

namespace EmbeddingShift.Adaptive
{
    /// <summary>
    /// Adaptive-level evaluation:
    /// Runs one or more evaluators on shifts proposed by a generator,
    /// selecting the best candidate shift for *this adaptive step*.
    /// 
    /// NOTE:
    /// This service is focused on *local decision making* in Adaptive.
    /// For *global benchmarking and logging* across datasets,
    /// see EvaluationRunner in the Evaluation namespace.
    /// </summary>
    public sealed class ShiftEvaluationService
    {
        private readonly IShiftGenerator _generator;
        private readonly IReadOnlyList<IShiftEvaluator> _evaluators;

        public ShiftEvaluationService(
            IShiftGenerator generator,
            IEnumerable<IShiftEvaluator>? evaluators = null)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _evaluators = (evaluators?.ToList() ?? new List<IShiftEvaluator>
            {
                new CosineSimilarityEvaluator()
            });
        }

        /// <summary>
        /// Evaluate shifts proposed by the generator against query/answer pairs.
        /// Returns only the *best shift per evaluator*.
        /// 
        /// This is "local" evaluation used inside Adaptive loops.
        /// </summary>
        public EvaluationReport Evaluate(
            IReadOnlyList<(ReadOnlyMemory<float> Query, ReadOnlyMemory<float> Answer)> pairs)
        {
            if (pairs == null || pairs.Count == 0)
                throw new ArgumentException("No pairs provided", nameof(pairs));

            var candidates = _generator.Generate(pairs).ToList();
            if (candidates.Count == 0)
                return new EvaluationReport(new List<EvaluatorResult>());

            var results = new List<EvaluatorResult>();

            foreach (var eval in _evaluators)
            {
                double bestScore = double.MinValue;
                IShift? bestShift = null;

                foreach (var shift in candidates)
                {
                    var result = eval.Evaluate(
                        shift,
                        pairs[0].Query.Span, // convention: first query for baseline
                        pairs.Select(p => p.Answer).ToList()
                    );

                    if (result.Score > bestScore)
                    {
                        bestScore = result.Score;
                        bestShift = shift;
                    }
                }

                results.Add(new EvaluatorResult(eval.GetType().Name, bestScore, bestShift));
            }

            return new EvaluationReport(results);
        }
    }

    /// <summary>
    /// Aggregated results of all evaluators (local best-shift selection).
    /// </summary>
    public sealed class EvaluationReport
    {
        public IReadOnlyList<EvaluatorResult> Results { get; }

        public EvaluationReport(IReadOnlyList<EvaluatorResult> results)
        {
            Results = results;
        }
    }

    /// <summary>
    /// Result of a single evaluator run (best score + chosen shift).
    /// </summary>
    public sealed class EvaluatorResult
    {
        public string Evaluator { get; }
        public double Score { get; }
        public IShift? BestShift { get; }

        public EvaluatorResult(string evaluator, double score, IShift? bestShift)
        {
            Evaluator = evaluator;
            Score = score;
            BestShift = bestShift;
        }
    }
}
