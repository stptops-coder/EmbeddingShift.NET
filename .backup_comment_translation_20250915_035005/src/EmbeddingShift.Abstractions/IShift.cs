namespace EmbeddingShift.Abstractions;

public interface IShift
{
    /// <summary>
    /// Wendet den Shift auf einen Eingabe-Embedding-Vektor an.
    /// Erwartet Länge = EmbeddingDimensions.DIM.
    /// </summary>
    float[] Apply(ReadOnlySpan<float> input);
}

