using System;
using System.Collections.Generic;
using EmbeddingShift.Abstractions;     // IShift, IShiftGenerator
using EmbeddingShift.Core.Shifts;      // AdditiveShift

namespace EmbeddingShift.Core.Generators
{
    /// <summary>
    /// Proposes an additive shift based on the average delta (Answer - Query)
    /// across all (Query, Answer) pairs.
    /// </summary>
    public sealed class DeltaShiftGenerator : IShiftGenerator
    {
        public IEnumerable<IShift> Generate(
            IReadOnlyList<(ReadOnlyMemory<float> Query, ReadOnlyMemory<float> Answer)> pairs)
        {
            if (pairs == null || pairs.Count == 0)
                yield break;

            int dim = pairs[0].Query.Length;
            var acc = new float[dim];

            foreach (var (Query, Answer) in pairs)
            {
                var q = Query.Span;
                var a = Answer.Span;

                if (q.Length != dim || a.Length != dim)
                    throw new ArgumentException("Dimension mismatch between query and answer.");

                for (int i = 0; i < dim; i++)
                    acc[i] += (a[i] - q[i]);   // accumulate (Answer - Query)
            }

            // average delta
            var delta = new float[dim];
            float inv = 1f / pairs.Count;
            for (int i = 0; i < dim; i++)
                delta[i] = acc[i] * inv;

            // single candidate: additive shift by average delta
            yield return new AdditiveShift(delta);
        }
    }
}
