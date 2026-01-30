using System;
using EmbeddingShift.Core.Infrastructure;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EmbeddingShift.Core.Runs;

namespace EmbeddingShift.ConsoleEval.Commands
{
    public static class RunsActiveCommand
    {
        public static Task RunAsync(string[] args)
        {
            // Usage:
            //   runs-active [--runs-root=<path>] [--domainKey=<key>] [--metric=<key>]
            //
            // Defaults:
            //   domainKey = insurance
            //   tenant    = ENV:EMBEDDINGSHIFT_TENANT (or "insurer-a")
            //   runs-root = .\results\<domainKey>\tenants\<tenant>\runs
            //   metric    = ndcg@3

            var runsRoot = GetOpt(args, "--runs-root");
            var domainKey = GetOpt(args, "--domainKey") ?? "insurance";
            var metricKey = GetOpt(args, "--metric") ?? "ndcg@3";

            if (string.IsNullOrWhiteSpace(runsRoot))
            {
                var tenant = Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_TENANT");
                if (string.IsNullOrWhiteSpace(tenant)) tenant = "insurer-a";
                Environment.SetEnvironmentVariable("EMBEDDINGSHIFT_TENANT", tenant);

                runsRoot = Path.Combine(DirectoryLayout.ResolveResultsRoot(domainKey), "runs");
            }

            if (!Directory.Exists(runsRoot))
            {
                Console.WriteLine($"[runs-active] Runs root not found: {runsRoot}");
                Environment.ExitCode = 1;
                return Task.CompletedTask;
            }

            if (!RunActivation.TryLoadActive(runsRoot, metricKey, out var pointer) || pointer is null)
            {
                Console.WriteLine($"[runs-active] No active pointer found for metric '{metricKey}'.");
                Console.WriteLine($"[runs-active] Tip: run 'runs-promote --metric={metricKey} --tenant <tenant>' first.");
                Environment.ExitCode = 0;
                return Task.CompletedTask;
            }

            Console.WriteLine($"[runs-active] root   = {runsRoot}");
            Console.WriteLine($"[runs-active] metric = {metricKey}");
            Console.WriteLine();
            Console.WriteLine($"Active directory: {pointer.RunDirectory}");
            Console.WriteLine($"WorkflowName    : {pointer.WorkflowName}");
            Console.WriteLine($"RunId           : {pointer.RunId}");
            Console.WriteLine($"Score           : {pointer.Score:0.000000}");
            Console.WriteLine($"run.json        : {pointer.RunJsonPath}");

            Environment.ExitCode = 0;
            return Task.CompletedTask;
        }

        private static string? GetOpt(string[] args, string key)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];

                if (a.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                    return a.Substring(key.Length + 1).Trim().Trim('"');

                if (string.Equals(a, key, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    return args[i + 1].Trim().Trim('"');
            }

            return null;
        }
    }
}
