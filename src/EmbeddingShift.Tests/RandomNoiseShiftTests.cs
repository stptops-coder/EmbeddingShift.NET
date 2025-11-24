using System;
using EmbeddingShift.Core.Shifts;
using Xunit;

namespace EmbeddingShift.Tests
{
    public class RandomNoiseShiftTests
    {
        [Fact]
        public void ApplyInPlace_with_positive_amplitude_changes_embedding()
        {
            var seed = 42;
            var rng = new Random(seed);

            var shift = new RandomNoiseShift(noiseAmplitude: 0.5f, rng: rng);

            var original = new float[16];
            for (var i = 0; i < original.Length; i++)
            {
                original[i] = 1.0f;
            }

            var embedding = (float[])original.Clone();

            shift.ApplyInPlace(embedding);

            // At least one dimension should have changed.
            var changed = false;
            for (var i = 0; i < embedding.Length; i++)
            {
                if (Math.Abs(embedding[i] - original[i]) > 1e-6)
                {
                    changed = true;
                    break;
                }
            }

            Assert.True(changed);
        }

        [Fact]
        public void ApplyInPlace_with_zero_amplitude_is_no_op()
        {
            var rng = new Random(123);
            var shift = new RandomNoiseShift(noiseAmplitude: 0.0f, rng: rng);

            var original = new float[8];
            for (var i = 0; i < original.Length; i++)
            {
                original[i] = 2.0f;
            }

            var embedding = (float[])original.Clone();

            shift.ApplyInPlace(embedding);

            for (var i = 0; i < embedding.Length; i++)
            {
                Assert.Equal(original[i], embedding[i]);
            }
        }

        [Fact]
        public void ApplyInPlace_with_same_seed_is_deterministic()
        {
            const int length = 32;
            const float amplitude = 0.3f;
            const int seed = 2025;

            var original = new float[length];
            for (var i = 0; i < original.Length; i++)
            {
                original[i] = 0.0f;
            }

            var embedding1 = (float[])original.Clone();
            var embedding2 = (float[])original.Clone();

            var shift1 = new RandomNoiseShift(amplitude, new Random(seed));
            var shift2 = new RandomNoiseShift(amplitude, new Random(seed));

            shift1.ApplyInPlace(embedding1);
            shift2.ApplyInPlace(embedding2);

            for (var i = 0; i < length; i++)
            {
                Assert.Equal(embedding1[i], embedding2[i], 5);
            }
        }
    }
}
