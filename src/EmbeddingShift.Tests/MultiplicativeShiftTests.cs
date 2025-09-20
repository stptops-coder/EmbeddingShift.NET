using System;
using EmbeddingShift.Core.Shifts;   // MultiplicativeShift
using FluentAssertions;
using Xunit;

namespace EmbeddingShift.Tests
{
    public sealed class MultiplicativeShiftTests
    {
        [Fact]
        public void Applies_Per_Dimension_Correctly()
        {
            var v = new float[] { 1f, 2f, -3f };
            var s = new MultiplicativeShift(new[] { 2f, 0.5f, 1f });

            var outV = s.Apply(v);
            outV.Should().BeEquivalentTo(new[] { 2f, 1f, -3f });
            BeFinite(outV);
        }

        [Fact]
        public void Clips_Factors_And_Guards_Small()
        {
            var v = new float[] { 1f, 1f, 1f };
            var s = new MultiplicativeShift(new[] { 100f, 0.01f, 0f }, clampAndGuard: true); // clamp + guard→1

            var outV = s.Apply(v);
            outV.Should().BeEquivalentTo(new[] { 4f, 0.25f, 1f });
            BeFinite(outV);
        }

        [Fact]
        public void Identity_Is_Idempotent()
        {
            var v = new float[] { 0.3f, -0.4f, 0.5f };
            var s = new MultiplicativeShift(new[] { 1f, 1f, 1f });

            var a = s.Apply(v);
            var b = s.Apply(a);
            ApproxEqual(a, v);
            ApproxEqual(b, v);
        }

        [Fact]
        public void No_NaN_On_Tiny_Values()
        {
            var v = new float[] { 1e-12f, -1e-12f, 0f };
            var s = new MultiplicativeShift(new[] { 1e-12f, 1e-12f, 1e-12f }); // guarded → ~1
            var outV = s.Apply(v);
            BeFinite(outV);
        }

        // --- helpers ---
        private static void BeFinite(ReadOnlySpan<float> v)
        {
            for (int i = 0; i < v.Length; i++)
            {
                float.IsNaN(v[i]).Should().BeFalse($"NaN at {i}");
                float.IsInfinity(v[i]).Should().BeFalse($"Inf at {i}");
            }
        }
        private static void ApproxEqual(ReadOnlySpan<float> a, ReadOnlySpan<float> b, float tol = 1e-6f)
        {
            a.Length.Should().Be(b.Length);
            for (int i = 0; i < a.Length; i++)
                Math.Abs(a[i] - b[i]).Should().BeLessThan(tol, $"dim {i}");
        }
    }
}
