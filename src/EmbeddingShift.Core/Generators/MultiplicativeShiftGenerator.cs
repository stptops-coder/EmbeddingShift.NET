using System;
using System.Collections.Generic;
using EmbeddingShift.Abstractions;      // IShift, IShiftGenerator
using EmbeddingShift.Core.Shifts;       // MultiplicativeShift

namespace EmbeddingShift.Core.Generators
{
    /// <summary>
    /// Proposes multiplicative shifts based on per-dimension factors derived
    /// from Answer / Query across all pairs. Uses geometric mean for stability.
    /// Returns multiple candidates (raw and tempered towards 1.0).
    /// </summary>
    public sealed class MultiplicativeShiftGenerator : IShiftGenerator
    {
        // Numerical guards
        private const float Eps = 1e-6f;          // avoid division by zero
        private const float MinFactor = 0.25f;    // clip to reasonable bounds
        private const float MaxFactor = 4.0f;

        public IEnumerable<IShift> Generate(
            IReadOnlyList<(ReadOnlyMemory<float> Query, ReadOnlyMemory<float> Answer)> pairs)
        {
            if (pairs == null || pairs.Count == 0)
                yield break;

            int dim = pairs[0].Query.Length;
            var logAcc = new double[dim]; // accumulate logs for geometric mean

            // Accumulate log(factor) with factor ≈ (|Answer|+Eps)/(|Query|+Eps)
            foreach (var (Query, Answer) in pairs)
            {
                var q = Query.Span;
                var a = Answer.Span;

                if (q.Length != dim || a.Length != dim)
                    throw new ArgumentException("Dimension mismatch between query and answer.");

                for (int i = 0; i < dim; i++)
                {
                    // Magnitude-only ratio is more stable across sign flips.
                    var num = Math.Abs(a[i]) + Eps;
                    var den = Math.Abs(q[i]) + Eps;
                    var ratio = num / den;

                    // Clamp before logging to avoid exploding logs.
                    if (ratio < MinFactor) ratio = MinFactor;
                    else if (ratio > MaxFactor) ratio = MaxFactor;

                    logAcc[i] += Math.Log(ratio);
                }
            }

            // Geometric mean per dimension
            var rawFactors = new float[dim];
            double invN = 1.0 / pairs.Count;
            for (int i = 0; i < dim; i++)
            {
                var gm = Math.Exp(logAcc[i] * invN); // geometric mean
                // Final safety clamp
                if (gm < MinFactor) gm = MinFactor;
                else if (gm > MaxFactor) gm = MaxFactor;
                rawFactors[i] = (float)gm;
            }

            // Candidate 1: raw geometric-mean factors
            yield return new MultiplicativeShift(rawFactors);

            // Candidate 2/3: tempered blends towards identity (1.0)
            yield return new MultiplicativeShift(BlendTowardsOne(rawFactors, 0.5f)); // 50% temper
            yield return new MultiplicativeShift(BlendTowardsOne(rawFactors, 0.25f)); // 25% temper
        }

        /// <summary>
        /// Blends factors towards 1.0: out = (1 - alpha) * 1 + alpha * factor.
        /// alpha in (0,1]; alpha=1 returns the original factors.
        /// </summary>
        private static float[] BlendTowardsOne(ReadOnlySpan<float> factors, float alpha)
        {
            var res = new float[factors.Length];
            float oneMinus = 1f - alpha;
            for (int i = 0; i < res.Length; i++)
                res[i] = oneMinus * 1f + alpha * factors[i];
            return res;
        }
    }
}
