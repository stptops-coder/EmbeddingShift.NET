namespace EmbeddingShift.Abstractions;

public interface IShift
{
    /// <summary>
    /// Applies the shift to an input embedding vector.
    /// Expects Length = EmbeddingDimensions.DIM.
    /// </summary>
    ReadOnlyMemory<float> Apply(ReadOnlySpan<float> input);
    string Name { get; }

}

