using System;
using System.Collections.Generic;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Evaluators
{
    public sealed class MarginEvaluator : BaseEvaluator
    {
        protected override EvaluationResult EvaluateCore(
            IShift shift,
            ReadOnlySpan<float> query,
            IReadOnlyList<ReadOnlyMemory<float>> refs)
        {
            var shifted = shift.Apply(query);

            float best = float.NegativeInfinity;
            float second = float.NegativeInfinity;

            for (int i = 0; i < refs.Count; i++)
            {
                var score = Cos(shifted, refs[i].Span);
                if (score > best)
                {
                    second = best;
                    best = score;
                }
                else if (score > second)
                {
                    second = score;
                }
            }

            var margin = (second == float.NegativeInfinity) ? 0f : (best - second);
            return new EvaluationResult(ShiftName(shift), margin, "Top1-Top2 margin");
        }
    }
}
