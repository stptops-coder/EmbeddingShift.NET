using EmbeddingShift.ConsoleEval.Inspector;
using EmbeddingShift.Core.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace EmbeddingShift.ConsoleEval.Commands
{
    /// <summary>
    /// Lists recent shift training results for a given workflow.
    /// </summary>
    public static class ShiftTrainingHistoryCommand
    {
        public static Task RunAsync(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: shift-training-history <workflowName> [maxItems] [domainKey]");
                return Task.CompletedTask;
            }

            var includeCancelled = false;

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
                Console.WriteLine("Usage: shift-training-history <workflowName> [maxItems] [domainKey] [--include-cancelled]");
                return Task.CompletedTask;
            }

            var workflowName = positional[0];

            int maxItems = 20;
            string domainKey = "insurance";

            if (positional.Count > 1)
            {
                if (int.TryParse(positional[1], out var parsed))
                    maxItems = parsed;
                else
                    domainKey = positional[1];
            }

            if (positional.Count > 2)
            {
                domainKey = positional[2];
            }

            var root = DirectoryLayout.ResolveResultsRoot(domainKey);

            ShiftTrainingResultInspector.PrintHistory(
                workflowName: workflowName,
                rootDirectory: root,
                maxItems: maxItems,
                includeCancelled: includeCancelled);

            return Task.CompletedTask;
        }
    }
}
