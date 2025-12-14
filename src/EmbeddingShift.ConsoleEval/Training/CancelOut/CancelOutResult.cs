namespace EmbeddingShift.Core.Training.CancelOut;

/// <summary>
/// Result of a cancel-out evaluation on a learned delta vector.
/// </summary>
public sealed record CancelOutResult(
    bool IsCancelled,
    string? Reason,
    float DeltaNorm
);
