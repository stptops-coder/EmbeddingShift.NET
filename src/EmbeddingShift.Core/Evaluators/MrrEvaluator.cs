using EmbeddingShift.Abstractions;
using EmbeddingShift.Core;

namespace EmbeddingShift.Core.Evaluators
{
    /// <summary>
    /// PURPOSE:
    ///   Mean Reciprocal Rank for the case where exactly one reference
    ///   is considered the single "relevant" answer.
    ///
    /// WHEN TO USE:
    ///   - Classic QA-style setup: one gold answer per query.
    ///   - You want to know how high the correct answer ranks post-shift.
    ///
    /// SCORE:
    ///   - Double in (0,1]; 1.0 if the relevant item is ranked first,
    ///     0.5 if second, etc. 0.0 if not found.
    /// </summary>
    public sealed class MrrEvaluator : IShiftEvaluator
    {
        private readonly int _relevantIndex;
        public MrrEvaluator(int relevantIndex = 0) => _relevantIndex = relevantIndex;

        public EvaluationResult Evaluate(
            IShift shift,
            ReadOnlySpan<float> query,
            IReadOnlyList<ReadOnlyMemory<float>> referenceEmbeddings)
        {
            if (referenceEmbeddings is null || referenceEmbeddings.Count == 0)
                return new EvaluationResult(nameof(MrrEvaluator), 0.0, "No references");

            Span<float> q = stackalloc float[query.Length];
            query.CopyTo(q);
            var shifted = shift.Apply(query);
            var shiftedSpan = shifted.Span;

            var scores = new List<(int idx, double s)>(referenceEmbeddings.Count);
            for (int i = 0; i < referenceEmbeddings.Count; i++)
                scores.Add((i, CoreVec.Cosine(shiftedSpan, referenceEmbeddings[i].Span)));

            scores.Sort((a, b) => b.s.CompareTo(a.s)); // desc
            int rank = scores.FindIndex(t => t.idx == _relevantIndex);
            double mrr = rank >= 0 ? 1.0 / (rank + 1) : 0.0;
            return new EvaluationResult(nameof(MrrEvaluator), mrr, $"rank={(rank >= 0 ? rank + 1 : -1)}");
        }
    }
}



