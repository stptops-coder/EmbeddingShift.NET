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

    
    // TODO [ZeroAlloc]: Consider adding an overload for in-place application to avoid allocations.
    // Example:
    // void Apply(ReadOnlySpan<float> input, Span<float> destination);
    //
    // This enables high-performance scenarios (e.g., batch evaluation loops) by reusing buffers
    // from ArrayPool<float>.Shared instead of allocating new arrays each call.


    /// <summary>
    /// Human-readable identifier of the shift (e.g., "NoShift.IngestBased", "Additive(Policy)", ReadOnlyMemory<float> Apply(ReadOnlySpan<float> input);).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Categorization to guide evaluators and adaptive selection (Baseline vs real shifts).
    /// </summary>
    ShiftKind Kind { get; }
}


