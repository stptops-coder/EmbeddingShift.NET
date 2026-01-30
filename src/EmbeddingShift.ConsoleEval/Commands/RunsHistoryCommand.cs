using System;
using EmbeddingShift.Core.Infrastructure;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EmbeddingShift.Core.Runs;

namespace EmbeddingShift.ConsoleEval.Commands
{
    public static class RunsHistoryCommand
    {
        public static Task RunAsync(string[] args)
        {
            // Usage:
            //   runs-history [--runs-root=<path>] [--domainKey=<key>] [--metric=<key>] [--max=N] [--exclude-preRollback] [--open]
            //
            // Defaults:
            //   domainKey = insurance
            //   tenant    = ENV:EMBEDDINGSHIFT_TENANT (or "insurer-a")
            //   runs-root = .\results\<domainKey>\tenants\<tenant>\runs
            //   metric    = ndcg@3
            //   max       = 20

            var runsRoot = GetOpt(args, "--runs-root");
            var domainKey = GetOpt(args, "--domainKey") ?? "insurance";
            var metricKey = GetOpt(args, "--metric") ?? "ndcg@3";
            var maxStr = GetOpt(args, "--max");
            var excludePreRollback = HasSwitch(args, "--exclude-preRollback");
            var open = HasSwitch(args, "--open");

            var max = 20;
            if (!string.IsNullOrWhiteSpace(maxStr) && int.TryParse(maxStr, out var parsed) && parsed > 0)
                max = parsed;

            if (string.IsNullOrWhiteSpace(runsRoot))
            {
                var tenant = Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_TENANT");
                if (string.IsNullOrWhiteSpace(tenant)) tenant = "insurer-a";
                Environment.SetEnvironmentVariable("EMBEDDINGSHIFT_TENANT", tenant);

                runsRoot = Path.Combine(DirectoryLayout.ResolveResultsRoot(domainKey), "runs");
            }

            if (!Directory.Exists(runsRoot))
            {
                Console.WriteLine($"[runs-history] Runs root not found: {runsRoot}");
                Environment.ExitCode = 1;
                return Task.CompletedTask;
            }

            var entries = RunActivation.ListHistory(
                runsRoot,
                metricKey,
                maxItems: max,
                includePreRollback: !excludePreRollback);

            var historyDir = Path.Combine(runsRoot, "_active", "history");

            Console.WriteLine($"[runs-history] root     = {runsRoot}");
            Console.WriteLine($"[runs-history] metric   = {metricKey}");
            Console.WriteLine($"[runs-history] max      = {max}");
            Console.WriteLine($"[runs-history] preRB    = {(!excludePreRollback ? "included" : "excluded")}");
            Console.WriteLine($"[runs-history] history  = {entries.Count}");
            Console.WriteLine($"[runs-history] dir      = {historyDir}");
            Console.WriteLine();

            if (entries.Count == 0)
            {
                Console.WriteLine("[runs-history] No history entries found.");
                Console.WriteLine("[runs-history] Tip: run 'runs-promote' at least twice, or run 'runs-rollback' once.");
                Environment.ExitCode = 0;
                return Task.CompletedTask;
            }

            Console.WriteLine("Rank | LastWriteUtc           | Kind        | Score     | RunId              | WorkflowName");
            Console.WriteLine("-----|-------------------------|------------|-----------|-------------------|------------------------------");

            var rank = 0;
            foreach (var e in entries)
            {
                rank++;

                var kind = e.IsPreRollback ? "preRollback" : "archived";
                var score = e.Pointer?.Score.ToString("0.000000") ?? "n/a";
                var runId = e.Pointer?.RunId ?? "n/a";
                var wf = e.Pointer?.WorkflowName ?? Path.GetFileName(e.Path);

                Console.WriteLine(
                    $"{rank,4} | {e.LastWriteUtc:yyyy-MM-dd HH:mm:ss}Z | {kind,-10} | {score,9} | {runId,-17} | {wf}");
            }

            Console.WriteLine();
            Console.WriteLine("Files:");
            foreach (var e in entries)
                Console.WriteLine($"  {e.Path}");

            if (open)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo(historyDir) { UseShellExecute = true };
                    System.Diagnostics.Process.Start(psi);
                }
                catch { /* ignore */ }
            }

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

        private static bool HasSwitch(string[] args, string key)
            => args.Any(a => string.Equals(a, key, StringComparison.OrdinalIgnoreCase));
    }
}
