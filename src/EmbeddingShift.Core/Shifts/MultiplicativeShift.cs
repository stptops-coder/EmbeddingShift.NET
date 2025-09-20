// using EmbeddingShift.Abstractions;

using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Shifts
{
    /// <summary>
    /// Element-wise multiplicative shift: output[i] = input[i] * factors[i].
    /// </summary>
    public sealed class MultiplicativeShift : IShift
    {
        private readonly float[] _factors;

        /// <summary>
        /// Creates a multiplicative shift with a uniform factor across all dimensions.
        /// </summary>
        public MultiplicativeShift(float factor, int dimensions)
        {
            if (dimensions <= 0) throw new ArgumentOutOfRangeException(nameof(dimensions));
            _factors = new float[dimensions];
            for (int i = 0; i < dimensions; i++) _factors[i] = factor;
        }

        /// <summary>
        /// Creates a multiplicative shift with per-dimension factors.
        /// The input array is defensively copied.
        /// </summary>
        public MultiplicativeShift(float[] factors)
        {
            if (factors is null) throw new ArgumentNullException(nameof(factors));
            _factors = (float[])factors.Clone(); // defensive copy
        }

        public float[] Apply(ReadOnlySpan<float> input)
        {
            if (input.Length != _factors.Length)
                throw new ArgumentException("Input length does not match factors length.", nameof(input));

            var result = new float[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                var f = _factors[i];
                // optional: guard against NaN/Infinity on factors
                // if (!float.IsFinite(f)) throw new ArgumentException("Invalid factor detected.", nameof(_factors));
                result[i] = input[i] * f;
            }
            return result;
        }

        public override string ToString() => $"MultiplicativeShift (Dims: {_factors.Length})";
    }
}
