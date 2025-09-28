using System.Collections.Generic;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Evaluators
{
    public static class EvaluatorCatalog
    {
        public static IReadOnlyList<IShiftEvaluator> Defaults { get; } = new IShiftEvaluator[]
        {
            new CosineMeanEvaluator(),
            new MarginEvaluator(),
        };
    }
}
