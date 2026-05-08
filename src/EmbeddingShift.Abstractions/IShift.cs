namespace EmbeddingShift.Abstractions;

public enum ShiftKind
{
    NoShift = 0,
    Heuristic = 1,
    Learned = 2,
    Composite = 3
}

public interface IShift
{
    /// <summary>
    /// Applies the shift to an input embedding vector (Length = EmbeddingDimensions.DIM).
    /// Returns a new ReadOnlyMemory&lt;float&gt; instance. Implementations may allocate.
    /// </summary>
    ReadOnlyMemory<float> Apply(ReadOnlySpan<float> input);

    
    // Optional performance extension for high-throughput batch evaluation:
    // an in-place Apply(ReadOnlySpan<float> input, Span<float> destination) overload could reuse buffers
    // from ArrayPool<float>.Shared instead of allocating a new result array per call.
    // The current public baseline keeps the simpler allocation-returning contract.


    /// <summary>
    /// Human-readable identifier of the shift (e.g., "NoShift.IngestBased", "Additive(Policy)").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Categorization to guide evaluators and adaptive selection (Baseline vs real shifts).
    /// </summary>
    ShiftKind Kind { get; }
}


