using System;
using System.Threading.Tasks;
using EmbeddingShift.ConsoleEval.Inspector;
using EmbeddingShift.Core.Infrastructure;

namespace EmbeddingShift.ConsoleEval.Commands
{
    /// <summary>
    /// Legacy alias for listing recent shift-training results.
    /// Prefer: domain mini-insurance shift-training-history
    /// </summary>
    internal static class MiniInsuranceTrainingListCommand
    {
        public static Task RunAsync(string[] args)
        {
            var workflowName = args.Length >= 1 ? args[0] : "mini-insurance-first-delta";

            var maxItems = 20;
            if (args.Length >= 2 && int.TryParse(args[1], out var parsed) && parsed > 0)
                maxItems = parsed;

            var domainKey = args.Length >= 3 ? args[2] : "insurance";

            Console.WriteLine();
            Console.WriteLine("=== Mini-Insurance · Training Runs (legacy alias) ===");
            Console.WriteLine($"Workflow   : {workflowName}");
            Console.WriteLine($"DomainKey  : {domainKey}");
            Console.WriteLine($"MaxItems   : {maxItems}");
            Console.WriteLine();

            var rootDirectory = DirectoryLayout.ResolveResultsRoot(domainKey);
            ShiftTrainingResultInspector.PrintHistory(workflowName, rootDirectory, maxItems);

            return Task.CompletedTask;
        }
    }
}
