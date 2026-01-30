using System;
using EmbeddingShift.Core.Infrastructure;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EmbeddingShift.Core.Runs;

namespace EmbeddingShift.ConsoleEval.Commands
{
    /// <summary>
    /// Compare persisted run artifacts (run.json) under a runs root.
    /// </summary>
    public static class RunsCompareCommand
    {
        private sealed record Ranked(
            int Rank,
            string WorkflowName,
            string RunId,
            double Score,
            double? Map1,
            double? Ndcg3,
            string RunDirectory);

        private sealed record RunComparisonArtifact(
            string MetricKey,
            DateTimeOffset CreatedUtc,
            string RunsRoot,
            int TotalRuns,
            IReadOnlyList<Ranked> Top);

        public static Task RunAsync(string[] args)
        {
            // Usage:
            //   runs-compare [--runs-root=<path>] [--domainKey=<key>] [--metric=<key>] [--top=N] [--write] [--out=<dir>]
            //
            // Defaults:
            //   domainKey = insurance
            //   tenant    = ENV:EMBEDDINGSHIFT_TENANT (or "insurer-a" if missing)
            //   runs-root = .\results\<domainKey>\tenants\<tenant>\runs
            //   metric    = ndcg@3
            //   top       = 20
            //   write     = false

            var runsRoot = GetOpt(args, "--runs-root");
            var domainKey = GetOpt(args, "--domainKey") ?? "insurance";
            var metricKey = GetOpt(args, "--metric") ?? "ndcg@3";
            var outDir = GetOpt(args, "--out");
            var write = HasSwitch(args, "--write");

            var topRaw = GetOpt(args, "--top");
            var top = 20;
            if (!string.IsNullOrWhiteSpace(topRaw) && int.TryParse(topRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTop))
                top = Math.Max(1, parsedTop);

            if (string.IsNullOrWhiteSpace(runsRoot))
            {
                var tenant = Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_TENANT");
                if (string.IsNullOrWhiteSpace(tenant)) tenant = "insurer-a";
                Environment.SetEnvironmentVariable("EMBEDDINGSHIFT_TENANT", tenant);

                runsRoot = Path.Combine(DirectoryLayout.ResolveResultsRoot(domainKey), "runs");
            }

            if (!Directory.Exists(runsRoot))
            {
                Console.WriteLine($"[runs-compare] Runs root not found: {runsRoot}");
                Console.WriteLine($"[runs-compare] Hint: pass --runs-root=<path> or ensure you run from repo root.");
                Environment.ExitCode = 1;
                return Task.CompletedTask;
            }

            var discovered = RunArtifactDiscovery.Discover(runsRoot);

            if (discovered.Count == 0)
            {
                Console.WriteLine($"[runs-compare] No run.json found under: {runsRoot}");
                Environment.ExitCode = 0;
                return Task.CompletedTask;
            }

            var ranked = discovered
                .Select(r =>
                {
                    var has = RunArtifactDiscovery.TryGetMetric(r.Artifact, metricKey, out var score);
                    var mapOk = RunArtifactDiscovery.TryGetMetric(r.Artifact, "map@1", out var map1);
                    var ndOk = RunArtifactDiscovery.TryGetMetric(r.Artifact, "ndcg@3", out var ndcg3);

                    return new
                    {
                        r.Artifact.WorkflowName,
                        r.Artifact.RunId,
                        Score = has ? score : double.NegativeInfinity,
                        Map1 = mapOk ? (double?)map1 : null,
                        Ndcg3 = ndOk ? (double?)ndcg3 : null,
                        r.RunDirectory
                    };
                })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.WorkflowName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(x => x.RunId, StringComparer.OrdinalIgnoreCase)
                .Take(top)
                .Select((x, i) => new Ranked(
                    Rank: i + 1,
                    WorkflowName: x.WorkflowName,
                    RunId: x.RunId,
                    Score: x.Score,
                    Map1: x.Map1,
                    Ndcg3: x.Ndcg3,
                    RunDirectory: x.RunDirectory))
                .ToList();

            PrintConsole(metricKey, runsRoot, discovered.Count, ranked);

            if (write)
            {
                if (string.IsNullOrWhiteSpace(outDir))
                    outDir = Path.Combine(runsRoot, "_compare");

                Directory.CreateDirectory(outDir);

                var safeMetric = SanitizeFileName(metricKey);
                var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);

                var mdPath = Path.Combine(outDir, $"compare_{safeMetric}_{stamp}.md");
                var jsonPath = Path.Combine(outDir, $"compare_{safeMetric}_{stamp}.json");

                var md = RenderMarkdown(metricKey, runsRoot, discovered.Count, ranked);
                File.WriteAllText(mdPath, md, new UTF8Encoding(false));

                var artifact = new RunComparisonArtifact(
                    MetricKey: metricKey,
                    CreatedUtc: DateTimeOffset.UtcNow,
                    RunsRoot: runsRoot,
                    TotalRuns: discovered.Count,
                    Top: ranked);

                var json = JsonSerializer.Serialize(
                    artifact,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });

                File.WriteAllText(jsonPath, json, new UTF8Encoding(false));

                Console.WriteLine();
                Console.WriteLine("[runs-compare] Wrote:");
                Console.WriteLine($"  {mdPath}");
                Console.WriteLine($"  {jsonPath}");
            }

            Environment.ExitCode = 0;
            return Task.CompletedTask;
        }

        private static void PrintConsole(string metricKey, string runsRoot, int total, IReadOnlyList<Ranked> ranked)
        {
            Console.WriteLine($"[runs-compare] root   = {runsRoot}");
            Console.WriteLine($"[runs-compare] metric = {metricKey}");
            Console.WriteLine($"[runs-compare] runs   = {total}");
            Console.WriteLine();

            Console.WriteLine("Rank | Score     | MAP@1     | NDCG@3    | WorkflowName");
            Console.WriteLine("-----|-----------|----------|----------|------------------------------");

            foreach (var r in ranked)
            {
                var score = double.IsNegativeInfinity(r.Score) ? "n/a" : r.Score.ToString("0.000000", CultureInfo.InvariantCulture);
                var map1 = r.Map1?.ToString("0.000000", CultureInfo.InvariantCulture) ?? "n/a";
                var ndcg3 = r.Ndcg3?.ToString("0.000000", CultureInfo.InvariantCulture) ?? "n/a";

                Console.WriteLine($"{r.Rank,4} | {score,9} | {map1,8} | {ndcg3,8} | {r.WorkflowName}");
            }
        }

        private static string RenderMarkdown(string metricKey, string runsRoot, int total, IReadOnlyList<Ranked> ranked)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"# Run comparison");
            sb.AppendLine();
            sb.AppendLine($"- Root: `{runsRoot}`");
            sb.AppendLine($"- Metric: `{metricKey}`");
            sb.AppendLine($"- Total runs found: `{total}`");
            sb.AppendLine();
            sb.AppendLine("| Rank | Score | MAP@1 | NDCG@3 | WorkflowName | RunId | RunDirectory |");
            sb.AppendLine("|---:|---:|---:|---:|---|---|---|");

            foreach (var r in ranked)
            {
                var score = double.IsNegativeInfinity(r.Score) ? "n/a" : r.Score.ToString("0.000000", CultureInfo.InvariantCulture);
                var map1 = r.Map1?.ToString("0.000000", CultureInfo.InvariantCulture) ?? "n/a";
                var ndcg3 = r.Ndcg3?.ToString("0.000000", CultureInfo.InvariantCulture) ?? "n/a";

                sb.AppendLine($"| {r.Rank} | {score} | {map1} | {ndcg3} | {r.WorkflowName} | {r.RunId} | `{r.RunDirectory}` |");
            }

            return sb.ToString();
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
