using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core;

/// <summary>Addiert einen Bias-Vektor auf das Embedding.</summary>
public sealed class AdditiveShift : IShift
{
    private readonly float[] _bias; // Länge = DIM

    public AdditiveShift(ReadOnlySpan<float> bias)
    {
        if (bias.Length != EmbeddingDimensions.DIM)
            throw new ArgumentException($"Bias must have length {EmbeddingDimensions.DIM}.", nameof(bias));
        _bias = bias.ToArray(); // defensive copy
    }

    public float[] Apply(ReadOnlySpan<float> input)
    {
        if (input.Length != EmbeddingDimensions.DIM)
            throw new ArgumentException($"Input must have length {EmbeddingDimensions.DIM}.", nameof(input));

        // elementweise Addition (SIMD inside Vec.Add)
        return Vec.Add(input, _bias);
    }
}

