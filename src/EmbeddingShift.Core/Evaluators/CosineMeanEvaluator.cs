using System;
using System.Collections.Generic;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Utils;

namespace EmbeddingShift.Core.Evaluators
{
    public sealed class CosineMeanEvaluator : BaseEvaluator
    {
        protected override EvaluationResult EvaluateCore(
            IShift shift,
            ReadOnlySpan<float> query,
            IReadOnlyList<ReadOnlyMemory<float>> refs)
        {
            var shiftedMem = shift.Apply(query);
            var shifted = shiftedMem.Span; double acc = 0d; int n = refs.Count;
            for (int i = 0; i < n; i++) acc += Cos(shifted, refs[i].Span);
            var avg = n > 0 ? (float)(acc / n) : 0f;
            return new EvaluationResult(ShiftName(shift), avg, "Cosine mean over references");
        }
    }
}

