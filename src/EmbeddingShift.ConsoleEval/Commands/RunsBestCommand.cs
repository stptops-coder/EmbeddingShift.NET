using System;
using EmbeddingShift.Core.Infrastructure;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EmbeddingShift.Core.Runs;

namespace EmbeddingShift.ConsoleEval.Commands
{
    public static class RunsBestCommand
    {
        private sealed record BestPointer(
            string MetricKey,
            DateTimeOffset CreatedUtc,
            string RunsRoot,
            int TotalRunsFound,
            string WorkflowName,
            string RunId,
            double Score,
            string RunDirectory,
            string RunJsonPath);

        public static Task RunAsync(string[] args)
        {
            // Usage:
            //   runs-best [--runs-root=<path>] [--domainKey=<key>] [--metric=<key>] [--write] [--out=<dir>] [--open]
            //
            // Defaults:
            //   domainKey = insurance
            //   tenant    = ENV:EMBEDDINGSHIFT_TENANT (or "insurer-a")
            //   runs-root = .\results\<domainKey>\tenants\<tenant>\runs
            //   metric    = ndcg@3

            var runsRoot = GetOpt(args, "--runs-root");
            var domainKey = GetOpt(args, "--domainKey") ?? "insurance";
            var metricKey = GetOpt(args, "--metric") ?? "ndcg@3";
            var outDir = GetOpt(args, "--out");
            var write = HasSwitch(args, "--write");
            var open = HasSwitch(args, "--open");

            if (string.IsNullOrWhiteSpace(runsRoot))
            {
                var tenant = Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_TENANT");
                if (string.IsNullOrWhiteSpace(tenant)) tenant = "insurer-a";
                Environment.SetEnvironmentVariable("EMBEDDINGSHIFT_TENANT", tenant);

                runsRoot = Path.Combine(DirectoryLayout.ResolveResultsRoot(domainKey), "runs");
            }

            if (!Directory.Exists(runsRoot))
            {
                Console.WriteLine($"[runs-best] Runs root not found: {runsRoot}");
                Environment.ExitCode = 1;
                return Task.CompletedTask;
            }

            var discovered = RunArtifactDiscovery.Discover(runsRoot);
            if (discovered.Count == 0)
            {
                Console.WriteLine($"[runs-best] No run.json found under: {runsRoot}");
                Environment.ExitCode = 0;
                return Task.CompletedTask;
            }

            var best = RunBestSelection.SelectBest(metricKey, discovered);
            if (best is null)
            {
                Console.WriteLine($"[runs-best] No runs contained metric '{metricKey}'.");
                Console.WriteLine($"[runs-best] Tip: try --metric=map@1 or check run.json contents.");
                Environment.ExitCode = 0;
                return Task.CompletedTask;
            }

            Console.WriteLine($"[runs-best] root   = {runsRoot}");
            Console.WriteLine($"[runs-best] metric = {metricKey}");
            Console.WriteLine($"[runs-best] runs   = {discovered.Count}");
            Console.WriteLine();
            Console.WriteLine($"Best directory: {best.Run.RunDirectory}");
            Console.WriteLine($"WorkflowName : {best.Run.Artifact.WorkflowName}");
            Console.WriteLine($"RunId        : {best.Run.Artifact.RunId}");
            Console.WriteLine($"Score        : {best.Score.ToString("0.000000", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"run.json     : {best.Run.RunJsonPath}");

            if (write)
            {
                if (string.IsNullOrWhiteSpace(outDir))
                    outDir = Path.Combine(runsRoot, "_best");

                Directory.CreateDirectory(outDir);

                var safeMetric = SanitizeFileName(metricKey);
                var path = Path.Combine(outDir, $"best_{safeMetric}.json");

                var pointer = new BestPointer(
                    MetricKey: metricKey,
                    CreatedUtc: DateTimeOffset.UtcNow,
                    RunsRoot: runsRoot,
                    TotalRunsFound: discovered.Count,
                    WorkflowName: best.Run.Artifact.WorkflowName,
                    RunId: best.Run.Artifact.RunId,
                    Score: best.Score,
                    RunDirectory: best.Run.RunDirectory,
                    RunJsonPath: best.Run.RunJsonPath);

                var json = JsonSerializer.Serialize(
                    pointer,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });

                File.WriteAllText(path, json, new UTF8Encoding(false));

                Console.WriteLine();
                Console.WriteLine($"[runs-best] Wrote: {path}");
            }

            if (open)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo(best.Run.RunDirectory)
                    {
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                catch
                {
                    // ignore
                }
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

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);

            foreach (var ch in name)
                sb.Append(invalid.Contains(ch) ? '_' : ch);

            return sb.ToString();
        }
    }
}
