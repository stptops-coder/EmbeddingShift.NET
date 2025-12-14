using EmbeddingShift.ConsoleEval.Inspector;
using EmbeddingShift.Core.Infrastructure;
using System;
using System.Collections.Generic;
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
                Console.WriteLine("Usage: shift-training-best <workflowName> [domainKey] [--include-cancelled]");
                return Task.CompletedTask;
            }

            var includeCancelled =
     false;

            var positional = new List<string>();
            foreach (var a in args)
            {
                if (a.Equals("--include-cancelled", StringComparison.OrdinalIgnoreCase))
                    includeCancelled = true;
                else
                    positional.Add(a);
            }

            if (positional.Count == 0)
            {
                Console.WriteLine("Usage: shift-training-best <workflowName> [domainKey] [--include-cancelled]");
                return Task.CompletedTask;
            }

            var workflowName = positional[0];
            var domainKey = positional.Count > 1 ? positional[1] : "insurance";

            var root = DirectoryLayout.ResolveResultsRoot(domainKey);

            ShiftTrainingResultInspector.PrintBest(
                workflowName: workflowName,
                rootDirectory: root,
                includeCancelled: includeCancelled);

            return Task.CompletedTask;
        }
    }
}
