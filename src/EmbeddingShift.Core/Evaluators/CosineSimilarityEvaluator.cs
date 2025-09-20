using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Evaluators
{
    /// <summary>
    /// Evaluates a shift by calculating the cosine similarity between
    /// (Shift(query)) and the reference embeddings.
    /// The returned EvaluationResult.Score: the higher, the better.
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

            // Apply the shift
            var shifted = shift.Apply(query);                  // IShift.Apply(ReadOnlySpan<float>) -> float[]
            var shiftedSpan = new ReadOnlySpan<float>(shifted);

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
