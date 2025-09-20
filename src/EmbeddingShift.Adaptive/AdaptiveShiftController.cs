using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Adaptive
{
    public sealed class AdaptiveShiftController
    {
        private readonly IShiftGenerator _generator;
        private readonly IShiftEvaluator _evaluator;

        public AdaptiveShiftController(IShiftGenerator generator, IShiftEvaluator evaluator)
        {
            _generator = generator;
            _evaluator = evaluator;
        }

        /// <summary>
        /// Selects from generated shifts those that yield the greatest average improvement:
        /// mean(Score_after - Score_before) across all pairs.
        /// </summary>
        /// <param name="pairs">List of (Query, Answer) embeddings.</param>
        /// <param name="before">Reference embeddings for the "before" comparison.</param>
        /// <param name="after">Reference embeddings for the "after" comparison.</param>
        /// <param name="topK">How many shifts to return (minimum 1).</param>
        public IShift[] ProposeAndSelect(
            IReadOnlyList<(ReadOnlyMemory<float> Query, ReadOnlyMemory<float> Answer)> pairs,
            IReadOnlyList<ReadOnlyMemory<float>> before,
            IReadOnlyList<ReadOnlyMemory<float>> after,
            int topK = 1)
        {
            if (pairs is null || pairs.Count == 0)
                throw new ArgumentException("pairs must contain at least one item.", nameof(pairs));
            if (before is null) throw new ArgumentNullException(nameof(before));
            if (after is null) throw new ArgumentNullException(nameof(after));

            // Generate shifts (generator already works with ReadOnly* types)
            var candidates = _generator.Generate(pairs).ToArray();


            // Evaluate each shift: average improvement (after - before) across all pairs
            var scored = new List<(IShift Shift, double Delta)>(candidates.Length);
            foreach (var s in candidates)
            {
                double sumDelta = 0.0;

                for (int i = 0; i < pairs.Count; i++)
                {
                    var q = pairs[i].Query.Span;

                    var beforeRes = _evaluator.Evaluate(s, q, before);
                    var afterRes = _evaluator.Evaluate(s, q, after);

                    // The higher, the better – improvement counts
                    sumDelta += (afterRes.Score - beforeRes.Score);
                }

                var meanDelta = sumDelta / pairs.Count;
                scored.Add((s, meanDelta));
            }

            // Sort descending by improvement
            scored.Sort((x, y) => y.Delta.CompareTo(x.Delta));

            // Return top-K (at least 1)
            var k = Math.Max(1, topK);
            return scored.Take(k).Select(t => t.Shift).ToArray();
        }
    }
}
