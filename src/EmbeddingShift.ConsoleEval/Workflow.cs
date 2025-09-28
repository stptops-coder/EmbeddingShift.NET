using System;
using System.Threading.Tasks;
using EmbeddingShift.Core.Evaluators;

namespace EmbeddingShift.ConsoleEval
{
    internal static class Workflow
    {
        public static async Task IngestAsync(string samplesPath, string dataset, string targetSlot)
            => await Task.CompletedTask;

        public static async Task EvalAsync(string dataset)
        {
            var evaluators = EvaluatorCatalog.Defaults;
            Console.WriteLine($"[Eval] Dataset='{dataset}', Evaluators={evaluators.Count}");
            foreach (var ev in evaluators)
                Console.WriteLine($"  - {ev.GetType().Name} ready");
            await Task.CompletedTask;
        }
    }
}
