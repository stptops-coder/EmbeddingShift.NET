using System;
using System.Threading.Tasks;
using EmbeddingShift.ConsoleEval.Inspector;
using EmbeddingShift.Core.Infrastructure;

namespace EmbeddingShift.ConsoleEval.Commands
{
    /// <summary>
    /// Generic console command to inspect the latest shift training result
    /// for a given workflow. Uses the file-based ShiftTrainingResult repository
    /// under /results/&lt;domainKey&gt; (default: "insurance").
    /// </summary>
    internal static class ShiftTrainingInspectCommand
    {
        public static Task RunAsync(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("=== Shift Training · Inspect Latest ===");
            Console.WriteLine();

            if (args.Length == 0)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  shift-training-inspect <workflowName> [domainKey]");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  shift-training-inspect mini-insurance-posneg");
                Console.WriteLine("  shift-training-inspect mini-insurance-first-delta insurance");
                Console.WriteLine();
                return Task.CompletedTask;
            }

            var workflowName = args[0];
            var domainKey = args.Length > 1 ? args[1] : "insurance";

            var rootDirectory = DirectoryLayout.ResolveResultsRoot(domainKey);

            ShiftTrainingResultInspector.PrintLatest(workflowName, rootDirectory);

            return Task.CompletedTask;
        }
    }
}
