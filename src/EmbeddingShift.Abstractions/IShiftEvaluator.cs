namespace EmbeddingShift.Abstractions;

public sealed record EvaluationResult(
    string ShiftName,
    double Score,
    string? Notes = null
);

public interface IShiftEvaluator
{
    /// <summary>
    /// Evaluates a shift relative to references (ground truth or "good matches").
    /// Return value: the higher, the better (e.g., cosine mean, NDCG, etc.).
    /// </summary>
    EvaluationResult Evaluate(
        IShift shift,
        ReadOnlySpan<float> query,
        IReadOnlyList<ReadOnlyMemory<float>> referenceEmbeddings
    );
}
