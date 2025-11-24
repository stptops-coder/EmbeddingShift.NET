using System;
using EmbeddingShift.Abstractions.Shifts;

namespace EmbeddingShift.Core.Shifts
{
    /// <summary>
    /// Shift that adds small random noise to each embedding dimension.
    ///
    /// This can be used to simulate stochastic embedding behavior similar
    /// to non-deterministic backends. The noise is drawn from a simple
    /// uniform distribution in [-noiseAmplitude, +noiseAmplitude].
    ///
    /// For deterministic tests, provide a seeded Random instance.
    /// For stochastic runs, rely on Random.Shared.
    /// </summary>
    public sealed class RandomNoiseShift : IEmbeddingShift
    {
        private readonly float _noiseAmplitude;
        private readonly Random _rng;

        public RandomNoiseShift(float noiseAmplitude, Random? rng = null)
        {
            if (noiseAmplitude < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(noiseAmplitude));
            }

            _noiseAmplitude = noiseAmplitude;
            _rng = rng ?? Random.Shared;
        }

        /// <summary>
        /// Noise is a delta-style modification on top of existing embeddings.
        /// </summary>
        public ShiftStage Stage => ShiftStage.Delta;

        public string Name => "RandomNoise";

        /// <summary>
        /// Neutral weight. Pipelines can later decide how to weight this shift
        /// relative to others if needed.
        /// </summary>
        public float Weight => 1.0f;

        public void ApplyInPlace(float[] embedding)
        {
            if (embedding == null)
            {
                throw new ArgumentNullException(nameof(embedding));
            }

            if (_noiseAmplitude <= 0f)
            {
                // Explicitly do nothing when amplitude is zero.
                return;
            }

            for (var i = 0; i < embedding.Length; i++)
            {
                // Simple uniform noise in [-_noiseAmplitude, +_noiseAmplitude].
                var u = _rng.NextDouble() * 2.0 - 1.0; // in [-1, 1]
                var noise = (float)(u * _noiseAmplitude);
                embedding[i] += noise;
            }
        }
    }
}
