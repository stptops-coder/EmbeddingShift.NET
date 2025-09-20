namespace EmbeddingShift.Abstractions;

public sealed record EvaluationResult(
    string ShiftName,
    double Score,
    string? Notes = null
);

public interface IShiftEvaluator
{
    /// <summary>
    /// Bewertet einen Shift relativ zu Referenzen (Ground-Truth oder „gute Treffer“).
    /// Rückgabewert: je höher, desto besser (z.B. Cosine-Mittelwert, NDCG, etc.).
    /// </summary>
    EvaluationResult Evaluate(
        IShift shift,
        ReadOnlySpan<float> query,
        IReadOnlyList<ReadOnlyMemory<float>> referenceEmbeddings
    );
}
