using System;
using EmbeddingShift.Abstractions;   // EmbeddingHelper
using EmbeddingShift.Core.Utils;
using FluentAssertions;
using Xunit;
using VectorOps = EmbeddingShift.Core.Utils.VectorOps;

namespace EmbeddingShift.Tests
{
    public sealed class AdditiveReality1536Tests
    {
        [Fact]
        public void Additive_Blend_Towards_Target_Boosts_Cosine_In_1536D()
        {
            const int D = 1536;
            var rnd = new Random(123);

            // Random normalized query/target
            var query = Normalize(RandVec(D, rnd));
            var target = Normalize(RandVec(D, rnd));

            // Baseline cosine
            var baseToTarget = VectorOps.Cosine(query, target);

            // Additive blend towards target: q' = Normalize((1-β)·q + β·target)
            const float beta = 0.50f; // 50% pull towards target
            var blended = new float[D];
            for (int i = 0; i < D; i++)
                blended[i] = (1 - beta) * query[i] + beta * target[i];
            var qPrime = Normalize(blended);

            var postToTarget = VectorOps.Cosine(qPrime, target);

            // Expect a clear improvement
            (postToTarget - baseToTarget).Should().BeGreaterThan(0.40f,
                $"base={baseToTarget:F6}, post={postToTarget:F6}");
            postToTarget.Should().BeGreaterThan(0.70f);

            // Diagnostics
            System.Diagnostics.Debug.WriteLine(
                $"[Additive] base={baseToTarget:F6}, post={postToTarget:F6}, delta={(postToTarget - baseToTarget):F6}");
        }

        // --- helpers ---
        private static float[] RandVec(int dim, Random rnd)
        {
            var v = new float[dim];
            for (int i = 0; i < dim; i++) v[i] = (float)(rnd.NextDouble() * 2.0 - 1.0); // [-1,1]
            return v;
        }

        private static float[] Normalize(ReadOnlySpan<float> v)
        {
            double n2 = 0; for (int i = 0; i < v.Length; i++) n2 += v[i] * v[i];
            var n = Math.Sqrt(Math.Max(n2, 1e-30));
            var r = new float[v.Length];
            for (int i = 0; i < v.Length; i++) r[i] = (float)(v[i] / n);
            return r;
        }
    }
}

