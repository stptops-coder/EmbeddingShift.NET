using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.ConsoleEval
{
    /// <summary>
    /// Serializable comparison object for "mini-insurance-first-delta".
    /// This is intentionally plain and close to a future DB schema.
    /// </summary>
    public sealed class MiniInsuranceFirstDeltaComparison
    {
        public string WorkflowName { get; init; } = "mini-insurance-first-delta";
        public DateTime CreatedUtc { get; init; }

        public string? BaselineRunDirectory { get; init; }
        public string? FirstShiftRunDirectory { get; init; }
        public string? FirstPlusDeltaRunDirectory { get; init; }

        public IReadOnlyList<MiniInsuranceMetricRow> Metrics { get; init; } =
            Array.Empty<MiniInsuranceMetricRow>();
    }

    /// <summary>
    /// One row in the metric comparison table.
    /// </summary>
    public sealed class MiniInsuranceMetricRow
    {
        public string Metric { get; init; } = string.Empty;

        public double Baseline { get; init; }
        public double First { get; init; }
        public double FirstPlusDelta { get; init; }

        public double DeltaFirstVsBaseline { get; init; }
        public double DeltaFirstPlusDeltaVsBaseline { get; init; }
    }

    /// <summary>
    /// Factory and persistence helpers for MiniInsuranceFirstDeltaComparison.
    /// Writes both JSON and Markdown so data and human-readable report stay in sync.
    /// </summary>
    internal static class MiniInsuranceFirstDeltaArtifacts
    {
        public static MiniInsuranceFirstDeltaComparison CreateComparison(
            WorkflowResult baseline,
            WorkflowResult first,
            WorkflowResult firstPlusDelta,
            string? baselineRunDirectory,
            string? firstRunDirectory,
            string? firstPlusDeltaRunDirectory)
        {
            var baselineMetrics = baseline.Metrics ?? new Dictionary<string, double>();
            var firstMetrics = first.Metrics ?? new Dictionary<string, double>();
            var firstPlusDeltaMetrics = firstPlusDelta.Metrics ?? new Dictionary<string, double>();

            var allKeys = new SortedSet<string>(baselineMetrics.Keys);
            allKeys.UnionWith(firstMetrics.Keys);
            allKeys.UnionWith(firstPlusDeltaMetrics.Keys);

            var rows = new List<MiniInsuranceMetricRow>();

            foreach (var key in allKeys)
            {
                baselineMetrics.TryGetValue(key, out var b);
                firstMetrics.TryGetValue(key, out var f);
                firstPlusDeltaMetrics.TryGetValue(key, out var fd);

                rows.Add(new MiniInsuranceMetricRow
                {
                    Metric = key,
                    Baseline = b,
                    First = f,
                    FirstPlusDelta = fd,
                    DeltaFirstVsBaseline = f - b,
                    DeltaFirstPlusDeltaVsBaseline = fd - b
                });
            }

            return new MiniInsuranceFirstDeltaComparison
            {
                WorkflowName = "mini-insurance-first-delta",
                CreatedUtc = DateTime.UtcNow,
                BaselineRunDirectory = baselineRunDirectory,
                FirstShiftRunDirectory = firstRunDirectory,
                FirstPlusDeltaRunDirectory = firstPlusDeltaRunDirectory,
                Metrics = rows
            };
        }

        /// <summary>
        /// Persists the comparison object as JSON plus a Markdown report
        /// in a timestamped subdirectory under the given base directory.
        /// Returns the created comparison directory path.
        /// </summary>
        public static string PersistComparison(
            string baseDirectory,
            MiniInsuranceFirstDeltaComparison comparison)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("Base directory must not be null or empty.", nameof(baseDirectory));

            const string StableName = "mini-insurance-first-delta";

            var runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var comparisonDir = Path.Combine(baseDirectory, $"{StableName}_{runId}");

            Directory.CreateDirectory(comparisonDir);

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            var jsonPath = Path.Combine(comparisonDir, "metrics-comparison.json");
            var json = JsonSerializer.Serialize(comparison, jsonOptions);
            File.WriteAllText(jsonPath, json, encoding);

            var markdownPath = Path.Combine(comparisonDir, "metrics-comparison.md");
            var markdown = BuildMarkdown(comparison);
            File.WriteAllText(markdownPath, markdown, encoding);

            return comparisonDir;
        }

        private static string BuildMarkdown(MiniInsuranceFirstDeltaComparison comparison)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Mini Insurance First/Delta Comparison");
            sb.AppendLine();
            sb.AppendLine($"Created (UTC): {comparison.CreatedUtc:O}");
            sb.AppendLine();
            sb.AppendLine("## Run Directories");
            sb.AppendLine();
            sb.AppendLine($"- Baseline: `{comparison.BaselineRunDirectory}`");
            sb.AppendLine($"- FirstShift: `{comparison.FirstShiftRunDirectory}`");
            sb.AppendLine($"- First+Delta: `{comparison.FirstPlusDeltaRunDirectory}`");
            sb.AppendLine();
            sb.AppendLine("## Metrics");
            sb.AppendLine();
            sb.AppendLine("| Metric | Baseline | First | First+Delta | ΔFirst-BL | ΔFirst+Delta-BL |");
            sb.AppendLine("|--------|----------|-------|-------------|-----------|-----------------|");

            foreach (var row in comparison.Metrics)
            {
                sb.AppendLine(
                    $"| {row.Metric} | " +
                    $"{row.Baseline:F3} | " +
                    $"{row.First:F3} | " +
                    $"{row.FirstPlusDelta:F3} | " +
                    $"{row.DeltaFirstVsBaseline:+0.000;-0.000;0.000} | " +
                    $"{row.DeltaFirstPlusDeltaVsBaseline:+0.000;-0.000;0.000} |");
            }

            sb.AppendLine();
            return sb.ToString();
        }
    }
}
