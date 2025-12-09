using EmbeddingShift.ConsoleEval.Inspector;
using EmbeddingShift.Core.Infrastructure;
using System;
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

            var workflowName = args[0];

            int maxItems = 20;
            string domainKey = "insurance";

            if (args.Length > 1)
            {
                if (int.TryParse(args[1], out var parsed))
                {
                    maxItems = parsed;
                }
                else
                {
                    domainKey = args[1];
                }
            }

            if (args.Length > 2)
            {
                domainKey = args[2];
            }

            var root = DirectoryLayout.ResolveResultsRoot(domainKey);

            ShiftTrainingResultInspector.PrintHistory(
                workflowName: workflowName,
                rootDirectory: root,
                maxItems: maxItems);

            return Task.CompletedTask;
        }
    }
}
