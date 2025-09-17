using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Shifts
{
    public sealed class MultiplicativeShift : IShift
    {
        private readonly float[] _factors;

        public MultiplicativeShift(float factor, int dimensions)
        {
            if (dimensions <= 0)
                throw new ArgumentOutOfRangeException(nameof(dimensions));

            _factors = new float[dimensions];
            for (int i = 0; i < dimensions; i++)
                _factors[i] = factor;
        }

        public MultiplicativeShift(float[] factors)
        {
            _factors = factors ?? throw new ArgumentNullException(nameof(factors));
        }

        public float[] Apply(ReadOnlySpan<float> input)
        {
            if (input.Length != _factors.Length)
                throw new ArgumentException("Input length does not match factors length.", nameof(input));

            var result = new float[input.Length];
            for (int i = 0; i < input.Length; i++)
                result[i] = input[i] * _factors[i];

            return result;
        }

        public override string ToString() => $"MultiplicativeShift (Dims: {_factors.Length})";
    }
}
