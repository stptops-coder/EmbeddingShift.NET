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
            //   out     = <runroot>\results\insurance\reports\summary.txt

            var runRoot = GetArgValue(args, "--runroot=")
                ?? Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_ROOT");

            if (string.IsNullOrWhiteSpace(runRoot))
                throw new InvalidOperationException(
                    "RunRoot not provided. Use --runroot=<path> or set EMBEDDINGSHIFT_ROOT.");

            runRoot = Path.GetFullPath(runRoot);

            EnsureRunRootLayout(runRoot, DomainKey);

            var casesPath = Path.Combine(runRoot, "cases.json");
            if (!File.Exists(casesPath))
                throw new FileNotFoundException($"cases.json not found under RunRoot: {casesPath}", casesPath);

            var outPath = GetArgValue(args, "--out=");
            if (string.IsNullOrWhiteSpace(outPath))
            {
                outPath = Path.Combine(runRoot, "results", DomainKey, "reports", "summary.txt");
            }
            else
            {
                outPath = Path.GetFullPath(outPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            }

            var rows = LoadCases(casesPath);
            var summary = BuildSummaryText(runRoot, rows);

            File.WriteAllText(outPath, summary, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Console.WriteLine("[RunRootSummarize] Wrote:");
            Console.WriteLine($"  {outPath}");
            return Task.CompletedTask;
        }

        private static void EnsureRunRootLayout(string runRoot, string domainKey)
        {
            if (!Directory.Exists(runRoot))
                Directory.CreateDirectory(runRoot);

            var domainRoot = Path.Combine(runRoot, "results", domainKey);
            Directory.CreateDirectory(domainRoot);

            // Standard contract folders (create even if empty)
            var standard = new[]
            {
                "datasets",
                "training",
                "runs",
                "reports",
                "experiments",
                "aggregates",
                "inspect"
            };

            foreach (var folder in standard)
            {
                Directory.CreateDirectory(Path.Combine(domainRoot, folder));
            }
        }

        private static List<CaseRow> LoadCases(string casesPath)
        {
            var json = File.ReadAllText(casesPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var rows = JsonSerializer.Deserialize<List<CaseRow>>(json, options);

            if (rows is null || rows.Count == 0)
                throw new InvalidOperationException($"cases.json contains no items: {casesPath}");

            return rows;
        }

        private static string BuildSummaryText(string runRoot, List<CaseRow> rows)
        {
            var inv = CultureInfo.InvariantCulture;

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

            // Stage table (means)
            sb.AppendLine("Stage Means (over all seeds)");
            sb.AppendLine("------------------------------------------------------------");
            sb.AppendLine("| Stage   | MAP@1 Base | MAP@1 PosNeg |   ΔMAP | NDCG@3 Base | NDCG@3 PosNeg |  ΔNDCG |");
            sb.AppendLine("|---------|------------|-------------|--------|-------------|--------------|--------|");

            foreach (var g in rows.GroupBy(r => r.runStage).OrderBy(g => g.Key))
            {
                var stage = g.Key;

                var mapB = g.Average(x => x.baseline_map1);
                var mapP = g.Average(x => x.posneg_map1);
                var dMap = g.Average(x => DeltaMap(x));


                var ndB = g.Average(x => x.baseline_ndcg3);
                var ndP = g.Average(x => x.posneg_ndcg3);
                var dNd = g.Average(x => DeltaNdcg(x));

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
                var dMapMin = g.Min(x => DeltaMap(x));
                var dMapMax = g.Max(x => DeltaMap(x));
                var dNdMin = g.Min(x => DeltaNdcg(x));
                var dNdMax = g.Max(x => DeltaNdcg(x));

                sb.AppendLine(
                    $"| stage-{stage:00} | {dMapMin,8:+0.000;-0.000;0.000} | {dMapMax,8:+0.000;-0.000;0.000} | {dNdMin,9:+0.000;-0.000;0.000} | {dNdMax,9:+0.000;-0.000;0.000} |");
            }

            sb.AppendLine();
            sb.AppendLine("Notes");
            sb.AppendLine("------------------------------------------------------------");
            sb.AppendLine("- This summary is derived from cases.json (runner output).");
            sb.AppendLine("- Standard folders under results/insurance/ are ensured by this command.");
            sb.AppendLine("============================================================");

            return sb.ToString();
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
