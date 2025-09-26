using EmbeddingShift.Abstractions;
using EmbeddingShift.Core;

namespace EmbeddingShift.Core.Evaluators
{
    /// <summary>
    /// PURPOSE:
    ///   Baseline alignment metric. Computes the mean cosine similarity between
    ///   Shift(query) and all reference embeddings; higher is better.
    ///
    /// WHEN TO USE:
    ///   - Default choice for quick, robust evaluation during development.
    ///   - When you don't have explicit “correct answer” labels or graded relevance.
    ///
    /// SCORE:
    ///   - Double in [-1, 1]. We typically see [0..1] for embedding spaces;
    ///     higher means better alignment after the shift.
    /// </summary>
    public sealed class CosineSimilarityEvaluator : IShiftEvaluator
    {
        public EvaluationResult Evaluate(
            IShift shift,
            ReadOnlySpan<float> query,
            IReadOnlyList<ReadOnlyMemory<float>> referenceEmbeddings)
        {
            if (referenceEmbeddings == null || referenceEmbeddings.Count == 0)
                return new EvaluationResult(ShiftName(shift), double.NaN, "No references");

            var shifted = shift.Apply(query);
            var shiftedSpan = shifted.Span;

            double sum = 0.0;
            double max = double.NegativeInfinity;

            for (int i = 0; i < referenceEmbeddings.Count; i++)
            {
                var refSpan = referenceEmbeddings[i].Span;
                if (refSpan.Length != shiftedSpan.Length)
                    throw new ArgumentException("Dimension mismatch between shifted query and a reference embedding.");

                var cos = EmbeddingHelper.CosineSimilarity(shiftedSpan, refSpan);
                sum += cos;
                if (cos > max) max = cos;
            }

            var mean = sum / referenceEmbeddings.Count;
            var notes = $"mean={mean:F4}; max={max:F4}; refs={referenceEmbeddings.Count}";
            return new EvaluationResult(ShiftName(shift), mean, notes);
        }

        private static string ShiftName(IShift shift) => shift.GetType().Name;
    }
}
