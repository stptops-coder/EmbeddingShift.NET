namespace EmbeddingShift.Core.Training.CancelOut;

/// <summary>
/// Evaluates whether a learned delta vector effectively cancels out
/// (e.g. near-zero magnitude after aggregation).
/// </summary>
public static class CancelOutEvaluator
{
    /// <summary>
    /// Evaluates cancel-out based on the L2 norm of the delta vector.
    /// </summary>
    /// <param name="delta">Aggregated delta vector (full embedding space).</param>
    /// <param name="epsilon">
    /// Threshold below which the delta is considered cancelled.
    /// Typical values: 1e-3 … 1e-2 depending on embedding scale.
    /// </param>
    public static CancelOutResult Evaluate(
        ReadOnlySpan<float> delta,
        float epsilon)
    {
        double sumSq = 0d;

        for (int i = 0; i < delta.Length; i++)
        {
            var v = delta[i];
            sumSq += v * v;
        }

        var norm = (float)Math.Sqrt(sumSq);

        if (norm < epsilon)
        {
            return new CancelOutResult(
                IsCancelled: true,
                Reason: $"Delta norm {norm:F6} below epsilon {epsilon:F6}",
                DeltaNorm: norm
            );
        }

        return new CancelOutResult(
            IsCancelled: false,
            Reason: null,
            DeltaNorm: norm
        );
    }
}
