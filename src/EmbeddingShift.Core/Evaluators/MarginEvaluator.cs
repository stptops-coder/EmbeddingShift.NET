using EmbeddingShift.Abstractions;
using EmbeddingShift.Core; // EmbeddingHelper

namespace EmbeddingShift.Adaptive.Evaluators
{
    /// <summary>
    /// PURPOSE:
    ///   Measures ranking stability by computing the margin (Top-1 - Top-2)
    ///   cosine scores after applying the shift.
    ///
    /// WHEN TO USE:
    ///   - You care about a confident Top-1 result (clear winner vs. runner-up).
    ///   - As a tie-breaker alongside mean cosine or nDCG/MRR.
    ///
    /// SCORE:
    ///   - Double, can be negative/positive. Larger positive values mean
    ///     stronger separation between the best and the second-best candidate.
    /// </summary>
    public sealed class MarginEvaluator : IShiftEvaluator
    {
        public EvaluationResult Evaluate(
            IShift shift,
            ReadOnlySpan<float> query,
            IReadOnlyList<ReadOnlyMemory<float>> referenceEmbeddings)
        {
            if (referenceEmbeddings is null || referenceEmbeddings.Count < 2)
                return new EvaluationResult(nameof(MarginEvaluator), double.NegativeInfinity, "Need >= 2 references");

            Span<float> q = stackalloc float[query.Length];
            query.CopyTo(q);
            var shifted = shift.Apply(q);
            var shiftedSpan = new ReadOnlySpan<float>(shifted);

            var scores = new List<double>(referenceEmbeddings.Count);
            foreach (var r in referenceEmbeddings)
                scores.Add(EmbeddingHelper.CosineSimilarity(shiftedSpan, r.Span));

            scores.Sort((a, b) => b.CompareTo(a)); // desc
            var margin = scores[0] - scores[1];
            return new EvaluationResult(nameof(MarginEvaluator), margin, $"top1={scores[0]:F4}; top2={scores[1]:F4}");
        }
    }
}
