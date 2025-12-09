using EmbeddingShift.ConsoleEval.Inspector;
using EmbeddingShift.Core.Infrastructure;
using System;
using System.Threading.Tasks;

namespace EmbeddingShift.ConsoleEval.Commands
{
    /// <summary>
    /// Shows the best shift training result for a given workflow.
    /// </summary>
    public static class ShiftTrainingBestCommand
    {
        public static Task RunAsync(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: shift-training-best <workflowName> [domainKey]");
                return Task.CompletedTask;
            }

            var workflowName = args[0];
            var domainKey = args.Length > 1 ? args[1] : "insurance";

            var root = DirectoryLayout.ResolveResultsRoot(domainKey);

            ShiftTrainingResultInspector.PrintBest(
                workflowName: workflowName,
                rootDirectory: root);

            return Task.CompletedTask;
        }
    }
}
