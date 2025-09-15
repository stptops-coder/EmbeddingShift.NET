namespace EmbeddingShift.Abstractions;

public interface IShift
{
    /// <summary>
    /// Applies\ the\ shift\ to\ an\ input\ embedding\ vector\.
    /// Erwartet Länge = EmbeddingDimensions.DIM.
    /// </summary>
    float[] Apply(ReadOnlySpan<float> input);
}

