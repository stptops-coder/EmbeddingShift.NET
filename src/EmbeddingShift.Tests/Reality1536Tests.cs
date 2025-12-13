using System;
using EmbeddingShift.Abstractions;   // EmbeddingHelper
using EmbeddingShift.Core.Utils;
using EmbeddingShift.Core.Shifts;    // MultiplicativeShift
using Xunit;
using VectorOps = EmbeddingShift.Core.Utils.VectorOps;

namespace EmbeddingShift.Tests
{
    public sealed class Reality1536Tests
    {
        [Fact]
        public void Multiplicative_Shift_Exactly_Aligns_With_Target_In_1536D()
        {
            const int D = 1536;
            var rnd = new Random(42);

            var query = new float[D];
            for (int i = 0; i < D; i++)
                query[i] = 0.5f + (float)rnd.NextDouble() * 0.5f;

            var scale = new float[D];
            for (int i = 0; i < D; i++)
                scale[i] = 0.5f + (float)rnd.NextDouble() * 1.5f;

            var target = new float[D];
            for (int i = 0; i < D; i++)
                target[i] = query[i] * scale[i];

            var qN = Normalize(query);
            var tN = Normalize(target);
            var baseToTarget = VectorOps.Cosine(qN, tN);

            var shift = new MultiplicativeShift(scale);

            // shift.Apply liefert jetzt ReadOnlyMemory<float>
            var qShifted = shift.Apply(query);

            // Zugriff als Span
            var qShiftedN = Normalize(qShifted.Span);

            var postToTarget = VectorOps.Cosine(qShiftedN, tN);

            Assert.True(postToTarget > 0.03f);
            Assert.True((postToTarget - baseToTarget) > 0.03f);

            // If you had BeApproximately(1.0f, 1e-5f):
            Assert.InRange(postToTarget, 1.0f - 1e-5f, 1.0f + 1e-5f);
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

