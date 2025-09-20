using System;
using EmbeddingShift.Abstractions;   // EmbeddingHelper
using EmbeddingShift.Core;
using EmbeddingShift.Core.Shifts;    // MultiplicativeShift
using FluentAssertions;
using Xunit;

namespace EmbeddingShift.Tests
{
    public sealed class Reality1536Tests
    {
        [Fact]
        public void Multiplicative_Shift_Exactly_Aligns_With_Target_In_1536D()
        {
            const int D = 1536;
            var rnd = new Random(42);

            // Query strictly positive, so pure multiplication can preserve direction
            var query = new float[D];
            for (int i = 0; i < D; i++)
                query[i] = 0.5f + (float)rnd.NextDouble() * 0.5f; // [0.5,1.0]

            // Scale per dimension chosen to be safely within clamp bounds
            var scale = new float[D];
            for (int i = 0; i < D; i++)
                scale[i] = 0.5f + (float)rnd.NextDouble() * 1.5f; // [0.5,2.0] ⊂ [0.25,4.0]

            // Target is exactly the scaled dimensions
            var target = new float[D];
            for (int i = 0; i < D; i++)
                target[i] = query[i] * scale[i];

            // Baseline similarity (before shift)
            var qN = Normalize(query);
            var tN = Normalize(target);
            var baseToTarget = EmbeddingHelper.CosineSimilarity(qN, tN);

            // Shift factors exactly = scale → Apply(query) == target (up to rounding)
            var shift = new MultiplicativeShift(scale);
            var qShifted = shift.Apply(query);
            var qShiftedN = Normalize(qShifted);

            var postToTarget = EmbeddingHelper.CosineSimilarity(qShiftedN, tN);

            // Baseline is already very high for scaled vectors; realistic improvement ~0.04–0.07
            (postToTarget - baseToTarget).Should().BeGreaterThan(0.03f,
                $"base={baseToTarget:F6}, post={postToTarget:F6}");
            postToTarget.Should().BeGreaterThan(0.999f); // numerically ~1.0
            float.IsNaN(postToTarget).Should().BeFalse();

            // Diagnostics (will show up in test output window if run with Debug enabled)
            System.Diagnostics.Debug.WriteLine(
                $"base={baseToTarget:F6}, post={postToTarget:F6}, delta={(postToTarget - baseToTarget):F6}");
        }

        private static float[] Normalize(ReadOnlySpan<float> v)
        {
            double n2 = 0; for (int i = 0; i < v.Length; i++) n2 += v[i] * v[i];
            var n = Math.Sqrt(n2);
            var r = new float[v.Length];
            if (n == 0) { v.CopyTo(r); return r; }
            for (int i = 0; i < v.Length; i++) r[i] = (float)(v[i] / n);
            return r;
        }
    }
}
