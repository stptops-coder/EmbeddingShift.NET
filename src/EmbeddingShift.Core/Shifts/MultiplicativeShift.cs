using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Shifts
{
    public sealed class MultiplicativeShift : IShift
    {
        private readonly float[] _factors;

        // Optional: zentrale Limits
        private const float MinFactor = 0.25f; // small clamp up
        private const float MaxFactor = 4.0f;  // large clamp down

        public MultiplicativeShift(float factor, int dimensions)
        {
            if (dimensions <= 0) throw new ArgumentOutOfRangeException(nameof(dimensions));
            _factors = new float[dimensions];
            for (int i = 0; i < dimensions; i++) _factors[i] = factor;
        }

        /// <summary>
        /// Per-dimension factors. When clampAndGuard = true:
        /// - f <= 0   → 1
        /// - f in (0, MinFactor) → MinFactor
        /// - f in (MaxFactor, ∞) → MaxFactor
        /// - otherwise keep f.
        /// </summary>
        public MultiplicativeShift(float[] factors, bool clampAndGuard = false)
        {
            if (factors is null) throw new ArgumentNullException(nameof(factors));
            _factors = (float[])factors.Clone(); // defensive copy

            if (clampAndGuard)
            {
                for (int i = 0; i < _factors.Length; i++)
                {
                    var f = _factors[i];

                    if (f <= 0f) f = 1f;                 // guard against collapse/negative
                    else if (f < MinFactor) f = MinFactor;        // clamp up
                    else if (f > MaxFactor) f = MaxFactor;        // clamp down

                    _factors[i] = f;
                }
            }
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
