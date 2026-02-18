using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EmbeddingShift.ConsoleEval.Commands
{
    internal static class MiniInsuranceRunRootSummarizeCommand
    {
        private const string DomainKey = "insurance";

        // Runner output (optional).
        private sealed record CaseRow(
            string dataset,
            int seed,
            int trainStage,
            int runStage,
            double baseline_map1,
            double posneg_map1,
            double? delta_map1,
            double baseline_ndcg3,
            double posneg_ndcg3,
            double? delta_ndcg3);

        // Fallback: training + aggregate + runs.
        private sealed record TrainingResult(
            string WorkflowName,
            DateTime CreatedUtc,
            string BaseDirectory,
            int ComparisonRuns,
            string? TrainingMode,
            double CancelOutEpsilon,
            bool IsCancelled,
            string? CancelReason,
            double DeltaNorm);

        private sealed record Aggregate(
            DateTime CreatedUtc,
            string BaseDirectory,
            int ComparisonCount,
            IReadOnlyList<AggregateMetricRow> Metrics);

        private sealed record AggregateMetricRow(
            string Metric,
            double AverageBaseline,
            double AverageFirst,
            double AverageFirstPlusDelta,
            double AverageDeltaFirstVsBaseline,
            double AverageDeltaFirstPlusDeltaVsBaseline);

        private sealed record RunArtifact(
            string RunId,
            string WorkflowName,
            DateTimeOffset StartedUtc,
            DateTimeOffset FinishedUtc,
            bool Success,
            Dictionary<string, double> Metrics,
            string? Notes);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static double DeltaMap(CaseRow r) =>
            r.delta_map1 ?? (r.posneg_map1 - r.baseline_map1);

        private static double DeltaNdcg(CaseRow r) =>
            r.delta_ndcg3 ?? (r.posneg_ndcg3 - r.baseline_ndcg3);

        public static Task RunAsync(string[] args)
        {
            // Usage:
            //   domain mini-insurance runroot-summarize [--runroot=<path>] [--out=<path>]
            //
            // Defaults:
            //   runroot = ENV:EMBEDDINGSHIFT_ROOT
            //   out     = <runroot>\results\insurance\tenants\<tenant>\reports\summary.txt  (if tenant layout exists)
            //             otherwise: <runroot>\results\insurance\reports\summary.txt

            var runRoot = GetArgValue(args, "--runroot=")
                ?? Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_ROOT");

            if (string.IsNullOrWhiteSpace(runRoot))
                throw new InvalidOperationException(
                    "RunRoot not provided. Use --runroot=<path> or set EMBEDDINGSHIFT_ROOT.");

            runRoot = Path.GetFullPath(runRoot);

            var domainRoot = EnsureDomainRoot(runRoot, DomainKey);
            var tenantRoots = FindTenantRoots(domainRoot);

            var casesPath = Path.Combine(runRoot, "cases.json");
            var hasCases = File.Exists(casesPath);

            var explicitOut = GetArgValue(args, "--out=");
            if (!string.IsNullOrWhiteSpace(explicitOut))
            {
                // Explicit out path: write a single summary file.
                var outPath = Path.GetFullPath(explicitOut);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

                var summary = hasCases
                    ? BuildCasesSummaryText(runRoot, LoadCases(casesPath))
                    : BuildFallbackSummaryText(runRoot, domainRoot, tenantRoots);

                File.WriteAllText(outPath, summary, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                Console.WriteLine("[RunRootSummarize] Wrote:");
                Console.WriteLine($"  {outPath}");
                return Task.CompletedTask;
            }

            // Default behavior: tenant-aware output (if tenants exist).
            if (tenantRoots.Count > 0)
            {
                foreach (var tenantRoot in tenantRoots)
                {
                    var reportsDir = Path.Combine(tenantRoot, "reports");
                    Directory.CreateDirectory(reportsDir);

                    var outPath = Path.Combine(reportsDir, "summary.txt");
                    var summary = hasCases
                        ? BuildCasesSummaryText(runRoot, LoadCases(casesPath))
                        : BuildFallbackSummaryText(runRoot, domainRoot, tenantRoots);

                    File.WriteAllText(outPath, summary, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    Console.WriteLine("[RunRootSummarize] Wrote:");
                    Console.WriteLine($"  {outPath}");
                }

                return Task.CompletedTask;
            }

            // Legacy (no tenant folders): write to results\insurance\reports.
            var legacyReports = Path.Combine(domainRoot, "reports");
            Directory.CreateDirectory(legacyReports);
            var legacyOut = Path.Combine(legacyReports, "summary.txt");

            var legacySummary = hasCases
                ? BuildCasesSummaryText(runRoot, LoadCases(casesPath))
                : BuildFallbackSummaryText(runRoot, domainRoot, tenantRoots);

            File.WriteAllText(legacyOut, legacySummary, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Console.WriteLine("[RunRootSummarize] Wrote:");
            Console.WriteLine($"  {legacyOut}");
            return Task.CompletedTask;
        }

        private static string EnsureDomainRoot(string runRoot, string domainKey)
        {
            var domainRoot = Path.Combine(runRoot, "results", domainKey);
            Directory.CreateDirectory(domainRoot);
            return domainRoot;
        }

        private static List<string> FindTenantRoots(string domainRoot)
        {
            var tenantsRoot = Path.Combine(domainRoot, "tenants");
            if (!Directory.Exists(tenantsRoot))
                return new List<string>();

            var dirs = Directory.GetDirectories(tenantsRoot, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return dirs.Length == 0 ? new List<string>() : dirs.ToList();
        }

        private static List<CaseRow> LoadCases(string casesPath)
        {
            var json = File.ReadAllText(casesPath);
            var rows = JsonSerializer.Deserialize<List<CaseRow>>(json, JsonOptions);

            if (rows is null || rows.Count == 0)
                throw new InvalidOperationException($"cases.json contains no items: {casesPath}");

            return rows;
        }

        private static string BuildCasesSummaryText(string runRoot, List<CaseRow> rows)
        {
            var distinctSeeds = rows.Select(r => r.seed).Distinct().OrderBy(x => x).ToArray();
            var distinctTrainStages = rows.Select(r => r.trainStage).Distinct().OrderBy(x => x).ToArray();
            var distinctRunStages = rows.Select(r => r.runStage).Distinct().OrderBy(x => x).ToArray();

            var sb = new StringBuilder();

            sb.AppendLine("============================================================");
            sb.AppendLine("RunRoot Summary (Mini-Insurance)");
            sb.AppendLine("============================================================");
            sb.AppendLine($"RunRoot     : {runRoot}");
            sb.AppendLine($"Rows        : {rows.Count}");
            sb.AppendLine($"Seeds       : {string.Join(", ", distinctSeeds)}");
            sb.AppendLine($"TrainStages : {string.Join(", ", distinctTrainStages.Select(s => $"stage-{s:00}"))}");
            sb.AppendLine($"RunStages   : {string.Join(", ", distinctRunStages.Select(s => $"stage-{s:00}"))}");
            sb.AppendLine();

            sb.AppendLine("Stage Means (over all seeds)");
            sb.AppendLine("------------------------------------------------------------");
            sb.AppendLine("| Stage   | MAP@1 Base | MAP@1 PosNeg |   ΔMAP | NDCG@3 Base | NDCG@3 PosNeg |  ΔNDCG |");
            sb.AppendLine("|---------|------------|-------------|--------|-------------|--------------|--------|");

            foreach (var g in rows.GroupBy(r => r.runStage).OrderBy(g => g.Key))
            {
                var stage = g.Key;

                var mapB = g.Average(x => x.baseline_map1);
                var mapP = g.Average(x => x.posneg_map1);
                var dMap = g.Average(DeltaMap);

                var ndB = g.Average(x => x.baseline_ndcg3);
                var ndP = g.Average(x => x.posneg_ndcg3);
                var dNd = g.Average(DeltaNdcg);

                sb.AppendLine(
                    $"| stage-{stage:00} | {mapB,10:0.000} | {mapP,11:0.000} | {dMap,6:+0.000;-0.000;0.000} | {ndB,11:0.000} | {ndP,12:0.000} | {dNd,6:+0.000;-0.000;0.000} |");
            }

            sb.AppendLine();
            sb.AppendLine("Stage Delta Ranges (min/max over all seeds)");
            sb.AppendLine("------------------------------------------------------------");
            sb.AppendLine("| Stage   | ΔMAP min | ΔMAP max | ΔNDCG min | ΔNDCG max |");
            sb.AppendLine("|---------|----------|----------|-----------|-----------|");

            foreach (var g in rows.GroupBy(r => r.runStage).OrderBy(g => g.Key))
            {
                var stage = g.Key;
                var dMapMin = g.Min(DeltaMap);
                var dMapMax = g.Max(DeltaMap);
                var dNdMin = g.Min(DeltaNdcg);
                var dNdMax = g.Max(DeltaNdcg);

                sb.AppendLine(
                    $"| stage-{stage:00} | {dMapMin,8:+0.000;-0.000;0.000} | {dMapMax,8:+0.000;-0.000;0.000} | {dNdMin,9:+0.000;-0.000;0.000} | {dNdMax,9:+0.000;-0.000;0.000} |");
            }

            sb.AppendLine();
            sb.AppendLine("Notes");
            sb.AppendLine("------------------------------------------------------------");
            sb.AppendLine("- This summary is derived from cases.json (runner output).");
            sb.AppendLine("============================================================");

            return sb.ToString();
        }

        private static string BuildFallbackSummaryText(string runRoot, string domainRoot, List<string> tenantRoots)
        {
            var sb = new StringBuilder();

            sb.AppendLine("============================================================");
            sb.AppendLine("RunRoot Summary (Mini-Insurance)");
            sb.AppendLine("============================================================");
            sb.AppendLine($"RunRoot      : {runRoot}");
            sb.AppendLine($"GeneratedUtc : {DateTime.UtcNow:O}");
            sb.AppendLine();

            if (tenantRoots.Count == 0)
            {
                sb.AppendLine("Layout       : legacy (no results/<domain>/tenants folder found)");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("Layout       : tenant");
                sb.AppendLine($"Tenants      : {string.Join(", ", tenantRoots.Select(Path.GetFileName))}");
                sb.AppendLine();
            }

            sb.AppendLine("Artifacts (best-effort fallback)");
            sb.AppendLine("------------------------------------------------------------");
            sb.AppendLine("cases.json is missing, so this summary is derived from:");
            sb.AppendLine("- training/*/shift-training-result.json");
            sb.AppendLine("- aggregates/*/metrics-aggregate.json");
            sb.AppendLine("- runs/*/run.json");
            sb.AppendLine();

            var targets = tenantRoots.Count > 0 ? tenantRoots : new List<string> { domainRoot };

            foreach (var root in targets)
            {
                var tenantName = tenantRoots.Count > 0 ? Path.GetFileName(root) : "(none)";

                sb.AppendLine("------------------------------------------------------------");
                sb.AppendLine($"Tenant        : {tenantName}");
                sb.AppendLine($"ResultsRoot    : {root}");

                // Datasets
                var datasetsRoot = Path.Combine(root, "datasets");
                if (Directory.Exists(datasetsRoot))
                {
                    var datasets = Directory.GetDirectories(datasetsRoot, "*", SearchOption.TopDirectoryOnly)
                        .Select(Path.GetFileName)
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    sb.AppendLine($"Datasets       : count={datasets.Length}");

                    foreach (var ds in datasets.Take(8))
                    {
                        var dsPath = Path.Combine(datasetsRoot, ds!);
                        var stages = new[] { "stage-00", "stage-01", "stage-02" };
                        var stageStates = stages
                            .Select(s => Directory.Exists(Path.Combine(dsPath, s)) ? "OK" : "-" )
                            .ToArray();

                        sb.AppendLine($"  - {ds}  [{string.Join(", ", stageStates)}]");
                    }

                    if (datasets.Length > 8)
                        sb.AppendLine($"  ... ({datasets.Length - 8} more)");
                }
                else
                {
                    sb.AppendLine("Datasets       : (missing)");
                }

                // Latest training
                var latestTraining = TryLoadLatestTraining(root);
                if (latestTraining != null)
                {
                    sb.AppendLine("Training       : latest");
                    sb.AppendLine($"  Workflow      : {latestTraining.WorkflowName}");
                    sb.AppendLine($"  CreatedUtc    : {latestTraining.CreatedUtc:O}");
                    sb.AppendLine($"  Mode          : {latestTraining.TrainingMode ?? "-"}");
                    sb.AppendLine($"  Cancelled     : {latestTraining.IsCancelled}");
                    if (latestTraining.IsCancelled && !string.IsNullOrWhiteSpace(latestTraining.CancelReason))
                        sb.AppendLine($"  CancelReason  : {latestTraining.CancelReason}");
                    sb.AppendLine($"  DeltaNorm     : {latestTraining.DeltaNorm:0.000000E+0}");
                }
                else
                {
                    sb.AppendLine("Training       : (none found)");
                }

                // Latest aggregate
                var latestAggregate = TryLoadLatestAggregate(root);
                if (latestAggregate != null)
                {
                    sb.AppendLine("Aggregate      : latest (first-delta)");
                    sb.AppendLine($"  CreatedUtc    : {latestAggregate.CreatedUtc:O}");
                    sb.AppendLine($"  Comparisons   : {latestAggregate.ComparisonCount}");

                    var mapRow = latestAggregate.Metrics.FirstOrDefault(r =>
                        string.Equals(r.Metric, "map@1", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(r.Metric, "MAP@1", StringComparison.OrdinalIgnoreCase));

                    var ndcgRow = latestAggregate.Metrics.FirstOrDefault(r =>
                        string.Equals(r.Metric, "ndcg@3", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(r.Metric, "NDCG@3", StringComparison.OrdinalIgnoreCase));

                    if (mapRow != null)
                    {
                        sb.AppendLine($"  MAP@1         : base={mapRow.AverageBaseline:0.000}, first={mapRow.AverageFirst:0.000}, first+Δ={mapRow.AverageFirstPlusDelta:0.000}, Δ(first+Δ)={mapRow.AverageDeltaFirstPlusDeltaVsBaseline:+0.000;-0.000;0.000}");
                    }

                    if (ndcgRow != null)
                    {
                        sb.AppendLine($"  NDCG@3        : base={ndcgRow.AverageBaseline:0.000}, first={ndcgRow.AverageFirst:0.000}, first+Δ={ndcgRow.AverageFirstPlusDelta:0.000}, Δ(first+Δ)={ndcgRow.AverageDeltaFirstPlusDeltaVsBaseline:+0.000;-0.000;0.000}");
                    }

                    if (mapRow == null && ndcgRow == null)
                        sb.AppendLine("  (no MAP@1 / NDCG@3 rows found in metrics-aggregate.json)");
                }
                else
                {
                    sb.AppendLine("Aggregate      : (none found)");
                }

                // Latest runs
                var latestRuns = TryLoadLatestRuns(root);
                if (latestRuns.Count > 0)
                {
                    sb.AppendLine("Runs           : latest per workflow");

                    foreach (var r in latestRuns.OrderBy(x => x.WorkflowName, StringComparer.OrdinalIgnoreCase))
                    {
                        var map = TryGetMetric(r.Metrics, "map@1");
                        var ndcg = TryGetMetric(r.Metrics, "ndcg@3");

                        var mapText = map.HasValue ? map.Value.ToString("0.000", CultureInfo.InvariantCulture) : "-";
                        var ndcgText = ndcg.HasValue ? ndcg.Value.ToString("0.000", CultureInfo.InvariantCulture) : "-";

                        sb.AppendLine($"  - {r.WorkflowName}  (MAP@1={mapText}, NDCG@3={ndcgText}, success={r.Success})");
                    }
                }
                else
                {
                    sb.AppendLine("Runs           : (none found)");
                }

                sb.AppendLine();
            }

            sb.AppendLine("Notes");
            sb.AppendLine("------------------------------------------------------------");
            sb.AppendLine("- cases.json is currently optional; this command will not fail if it is missing.");
            sb.AppendLine("- For more detail, use: scripts/inspect/Inspect-RunRoot.ps1 -WriteJsonIndex");
            sb.AppendLine("============================================================");

            return sb.ToString();
        }

        private static TrainingResult? TryLoadLatestTraining(string resultsRoot)
        {
            try
            {
                var trainingRoot = Path.Combine(resultsRoot, "training");
                if (!Directory.Exists(trainingRoot))
                    return null;

                var candidates = Directory.GetDirectories(trainingRoot, "*-training_*", SearchOption.TopDirectoryOnly);
                if (candidates.Length == 0)
                    return null;

                Array.Sort(candidates, StringComparer.Ordinal);
                Array.Reverse(candidates);

                foreach (var dir in candidates)
                {
                    var jsonPath = Path.Combine(dir, "shift-training-result.json");
                    if (!File.Exists(jsonPath))
                    {
                        var legacy = Path.Combine(dir, "result.json");
                        if (!File.Exists(legacy))
                            continue;

                        jsonPath = legacy;
                    }

                    var json = File.ReadAllText(jsonPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    var result = JsonSerializer.Deserialize<TrainingResult>(json, JsonOptions);
                    if (result != null)
                        return result;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static Aggregate? TryLoadLatestAggregate(string resultsRoot)
        {
            try
            {
                var aggRoot = Path.Combine(resultsRoot, "aggregates");
                if (!Directory.Exists(aggRoot))
                    return null;

                var dirs = Directory.GetDirectories(aggRoot, "mini-insurance-first-delta-aggregate_*", SearchOption.TopDirectoryOnly);
                if (dirs.Length == 0)
                    return null;

                Array.Sort(dirs, StringComparer.Ordinal);
                Array.Reverse(dirs);

                foreach (var dir in dirs)
                {
                    var jsonPath = Path.Combine(dir, "metrics-aggregate.json");
                    if (!File.Exists(jsonPath))
                        continue;

                    var json = File.ReadAllText(jsonPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    var aggregate = JsonSerializer.Deserialize<Aggregate>(json, JsonOptions);
                    if (aggregate != null)
                        return aggregate;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static List<RunArtifact> TryLoadLatestRuns(string resultsRoot)
        {
            try
            {
                var runsRoot = Path.Combine(resultsRoot, "runs");
                if (!Directory.Exists(runsRoot))
                    return new List<RunArtifact>();

                var dirs = Directory.GetDirectories(runsRoot, "*", SearchOption.TopDirectoryOnly);
                if (dirs.Length == 0)
                    return new List<RunArtifact>();

                // Load all artifacts, then keep latest per workflow (by FinishedUtc).
                var artifacts = new List<RunArtifact>();

                foreach (var dir in dirs)
                {
                    var runJson = Path.Combine(dir, "run.json");
                    if (!File.Exists(runJson))
                        continue;

                    try
                    {
                        var json = File.ReadAllText(runJson, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                        var a = JsonSerializer.Deserialize<RunArtifact>(json, JsonOptions);
                        if (a != null)
                            artifacts.Add(a);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                return artifacts
                    .GroupBy(a => a.WorkflowName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(x => x.FinishedUtc).First())
                    .ToList();
            }
            catch
            {
                return new List<RunArtifact>();
            }
        }

        private static double? TryGetMetric(Dictionary<string, double> metrics, string key)
        {
            if (metrics == null || metrics.Count == 0)
                return null;

            if (metrics.TryGetValue(key, out var v))
                return v;

            // Common alternatives.
            if (metrics.TryGetValue(key.ToUpperInvariant(), out v))
                return v;

            if (metrics.TryGetValue(key.ToLowerInvariant(), out v))
                return v;

            return null;
        }

        private static string? GetArgValue(string[] args, string prefix)
        {
            foreach (var a in args)
            {
                if (a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return a.Substring(prefix.Length).Trim().Trim('"');
            }

            return null;
        }
    }
}
