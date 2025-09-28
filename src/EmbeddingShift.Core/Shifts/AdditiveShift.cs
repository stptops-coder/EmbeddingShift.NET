using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Shifts;

///\ <summary>Adds a bias vector to the embedding.</summary>
public sealed class AdditiveShift : IShift
{
    private readonly float[] _bias; // Länge = DIM
    public string Name => "AdditiveShift";


    public AdditiveShift(ReadOnlySpan<float> bias)
    {
        if (bias.Length != EmbeddingDimensions.DIM)
            throw new ArgumentException($"Bias must have length {EmbeddingDimensions.DIM}.", nameof(bias));
        _bias = bias.ToArray(); // defensive\ copy
    }

    public ReadOnlyMemory<float> Apply(ReadOnlySpan<float> input)
    {
        if (input.Length != EmbeddingDimensions.DIM)
            throw new ArgumentException($"Input must have length {EmbeddingDimensions.DIM}.", nameof(input));

        // element-wise Addition (SIMD\ inside\ Vec\.Add)
        return Vec.Add(input, _bias);
    }
}


