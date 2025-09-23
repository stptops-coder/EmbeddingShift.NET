using EmbeddingShift.Abstractions;
using EmbeddingShift.Core;

namespace EmbeddingShift.Adaptive.Evaluators
{
    /// <summary>
    /// PURPOSE:
    ///   Normalized DCG@K with binary relevance. Evaluates the *quality of the
    ///   ranking* (not just similarity) after shifting the query.
    ///
    /// WHEN TO USE:
    ///   - There may be multiple relevant references.
    ///   - You want top-K ranking quality, not just a single best match.
    ///
    /// SCORE:
    ///   - Double in [0,1]. 1.0 means the ranking is ideal for the given K
    ///     and the set of relevant items.
    ///     
    /// NOTE:
    ///   DCG (Discounted Cumulative Gain) rewards relevant results more if they
    ///   appear early in the ranking; later hits are discounted by log2.
    ///   nDCG normalizes this against the ideal ranking for comparability.
    /// </summary>
    public sealed class NdcgEvaluator : IShiftEvaluator
    {
        private readonly HashSet<int> _relevant;
        private readonly int _k;

        public NdcgEvaluator(IEnumerable<int> relevantIndices, int k = 10)
        {
            _relevant = new HashSet<int>(relevantIndices ?? Array.Empty<int>());
            _k = Math.Max(1, k);
        }

        public EvaluationResult Evaluate(
            IShift shift,
            ReadOnlySpan<float> query,
            IReadOnlyList<ReadOnlyMemory<float>> referenceEmbeddings)
        {
            if (referenceEmbeddings is null || referenceEmbeddings.Count == 0)
                return new EvaluationResult(nameof(NdcgEvaluator), 0.0, "No references");

            Span<float> q = stackalloc float[query.Length];
            query.CopyTo(q);
            var shifted = shift.Apply(q);
            var shiftedSpan = new ReadOnlySpan<float>(shifted);

            var scores = new List<(int idx, double s)>(referenceEmbeddings.Count);
            for (int i = 0; i < referenceEmbeddings.Count; i++)
                scores.Add((i, EmbeddingHelper.CosineSimilarity(shiftedSpan, referenceEmbeddings[i].Span)));
            scores.Sort((a, b) => b.s.CompareTo(a.s)); // desc

            double dcg = 0;
            int limit = Math.Min(_k, scores.Count);
            for (int r = 0; r < limit; r++)
            {
                int idx = scores[r].idx;
                int rel = _relevant.Contains(idx) ? 1 : 0;
                if (rel > 0) dcg += 1.0 / Math.Log2(r + 2); // rank r -> position r+1
            }

            int idealOnes = Math.Min(_relevant.Count, limit);
            double idcg = 0;
            for (int r = 0; r < idealOnes; r++)
                idcg += 1.0 / Math.Log2(r + 2);

            double ndcg = idcg > 0 ? dcg / idcg : 0.0;
            return new EvaluationResult(nameof(NdcgEvaluator), ndcg, $"K={_k}; relevant={_relevant.Count}");
        }
    }
}
