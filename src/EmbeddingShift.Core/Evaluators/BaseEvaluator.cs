using System;
using System.Collections.Generic;
//// harmless if missing
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Utils;
using VectorOps = EmbeddingShift.Core.Utils.VectorOps;

namespace EmbeddingShift.Core.Evaluators
{
    public abstract class BaseEvaluator : IShiftEvaluator
    {
        public EvaluationResult Evaluate(
            IShift shift,
            ReadOnlySpan<float> query,
            IReadOnlyList<ReadOnlyMemory<float>> referenceEmbeddings)
        {
            if (shift is null) throw new ArgumentNullException(nameof(shift));
            if (referenceEmbeddings is null || referenceEmbeddings.Count == 0)
                return new EvaluationResult(ShiftName(shift), 0f, "No references.");
            return EvaluateCore(shift, query, referenceEmbeddings);
        }

        protected abstract EvaluationResult EvaluateCore(
            IShift shift,
            ReadOnlySpan<float> query,
            IReadOnlyList<ReadOnlyMemory<float>> refs);

        protected static string ShiftName(IShift shift) => shift.GetType().Name;
        protected static float Cos(ReadOnlySpan<float> a, ReadOnlySpan<float> b) => VectorOps.Cosine(a, b);
    }
}

