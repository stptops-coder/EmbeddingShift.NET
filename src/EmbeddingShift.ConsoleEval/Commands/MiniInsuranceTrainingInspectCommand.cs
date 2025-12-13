using System;
using System.Threading.Tasks;
using EmbeddingShift.ConsoleEval.Inspector;
using EmbeddingShift.Core.Infrastructure;

namespace EmbeddingShift.ConsoleEval.Commands
{
    /// <summary>
    /// Legacy alias for inspecting the latest shift-training result.
    /// Prefer: domain mini-insurance shift-training-inspect
    /// </summary>
    internal static class MiniInsuranceTrainingInspectCommand
    {
        public static Task RunAsync(string[] args)
        {
            var workflowName = args.Length >= 1 ? args[0] : "mini-insurance-first-delta";
            var domainKey = args.Length >= 2 ? args[1] : "insurance";

            Console.WriteLine();
            Console.WriteLine("=== Mini-Insurance · Training Inspect (legacy alias) ===");
            Console.WriteLine($"Workflow   : {workflowName}");
            Console.WriteLine($"DomainKey  : {domainKey}");
            Console.WriteLine();

            var rootDirectory = DirectoryLayout.ResolveResultsRoot(domainKey);
            ShiftTrainingResultInspector.PrintLatest(workflowName, rootDirectory);

            return Task.CompletedTask;
        }
    }
}
