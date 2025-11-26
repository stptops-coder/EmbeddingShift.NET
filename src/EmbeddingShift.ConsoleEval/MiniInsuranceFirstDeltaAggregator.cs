using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace EmbeddingShift.ConsoleEval
{
    /// <summary>
    /// Aggregate object for many mini-insurance-first-delta comparison runs.
    /// This is intentionally close to a potential DB schema.
    /// </summary>
    public sealed class MiniInsuranceFirstDeltaAggregate
    {
        public DateTime CreatedUtc { get; init; }
        public string BaseDirectory { get; init; } = string.Empty;
        public int ComparisonCount { get; init; }

        public IReadOnlyList<MiniInsuranceAggregateMetricRow> Metrics { get; init; }
            = Array.Empty<MiniInsuranceAggregateMetricRow>();
    }

    /// <summary>
    /// One aggregated metric row (averages over all comparison runs).
    /// </summary>
    public sealed class MiniInsuranceAggregateMetricRow
    {
        public string Metric { get; init; } = string.Empty;

        public double AverageBaseline { get; init; }
        public double AverageFirst { get; init; }
        public double AverageFirstPlusDelta { get; init; }

        public double AverageDeltaFirstVsBaseline { get; init; }
        public double AverageDeltaFirstPlusDeltaVsBaseline { get; init; }
    }

    /// <summary>
    /// Reads many metrics-comparison.json files for mini-insurance-first-delta and
    /// produces a single aggregate (JSON + Markdown).
    /// </summary>
    public static class MiniInsuranceFirstDeltaAggregator
    {
        public static MiniInsuranceFirstDeltaAggregate AggregateFromDirectory(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("Base directory must not be null or empty.", nameof(baseDirectory));

            var comparisonDirs = Directory.GetDirectories(
                baseDirectory,
                "mini-insurance-first-delta_*",
                SearchOption.TopDirectoryOnly);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            // metric -> accumulator
            var metricAccumulators =
                new Dictionary<string, (double baseline, double first, double firstDelta, double dFirst, double dFirstDelta, int count)>();

            var comparisonCount = 0;

            foreach (var dir in comparisonDirs)
            {
                var jsonPath = Path.Combine(dir, "metrics-comparison.json");
                if (!File.Exists(jsonPath))
                    continue;

                var json = File.ReadAllText(jsonPath, encoding);
                var comparison = JsonSerializer.Deserialize<MiniInsuranceFirstDeltaComparison>(json, jsonOptions);
                if (comparison?.Metrics == null)
                    continue;

                comparisonCount++;

                foreach (var row in comparison.Metrics)
                {
                    if (!metricAccumulators.TryGetValue(row.Metric, out var acc))
                    {
                        acc = (0, 0, 0, 0, 0, 0);
                    }

                    acc.baseline += row.Baseline;
                    acc.first += row.First;
                    acc.firstDelta += row.FirstPlusDelta;
                    acc.dFirst += row.DeltaFirstVsBaseline;
                    acc.dFirstDelta += row.DeltaFirstPlusDeltaVsBaseline;
                    acc.count++;

                    metricAccumulators[row.Metric] = acc;
                }
            }

            if (comparisonCount == 0 || metricAccumulators.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No 'metrics-comparison.json' files found under '{baseDirectory}'. " +
                    "Run 'mini-insurance-first-delta' at least once before aggregating.");
            }

            var metricRows = new List<MiniInsuranceAggregateMetricRow>();

            foreach (var kvp in metricAccumulators)
            {
                var name = kvp.Key;
                var acc = kvp.Value;

                if (acc.count == 0)
                    continue;

                var denom = (double)acc.count;

                metricRows.Add(new MiniInsuranceAggregateMetricRow
                {
                    Metric = name,
                    AverageBaseline = acc.baseline / denom,
                    AverageFirst = acc.first / denom,
                    AverageFirstPlusDelta = acc.firstDelta / denom,
                    AverageDeltaFirstVsBaseline = acc.dFirst / denom,
                    AverageDeltaFirstPlusDeltaVsBaseline = acc.dFirstDelta / denom
                });
            }

            metricRows.Sort((a, b) => string.CompareOrdinal(a.Metric, b.Metric));

            return new MiniInsuranceFirstDeltaAggregate
            {
                CreatedUtc = DateTime.UtcNow,
                BaseDirectory = baseDirectory,
                ComparisonCount = comparisonCount,
                Metrics = metricRows
            };
        }

        /// <summary>
        /// Persists the aggregate as JSON plus Markdown in a new subdirectory
        /// under the given base directory.
        /// </summary>
        public static string PersistAggregate(string baseDirectory, MiniInsuranceFirstDeltaAggregate aggregate)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("Base directory must not be null or empty.", nameof(baseDirectory));

            var runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var aggregateDir = Path.Combine(baseDirectory, $"mini-insurance-first-delta-aggregate_{runId}");

            Directory.CreateDirectory(aggregateDir);

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            var jsonPath = Path.Combine(aggregateDir, "metrics-aggregate.json");
            var json = JsonSerializer.Serialize(aggregate, jsonOptions);
            File.WriteAllText(jsonPath, json, encoding);

            var markdownPath = Path.Combine(aggregateDir, "metrics-aggregate.md");
            var markdown = BuildMarkdown(aggregate);
            File.WriteAllText(markdownPath, markdown, encoding);

            return aggregateDir;
        }

        private static string BuildMarkdown(MiniInsuranceFirstDeltaAggregate aggregate)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Mini Insurance First/Delta Aggregate Metrics");
            sb.AppendLine();
            sb.AppendLine($"Created (UTC): {aggregate.CreatedUtc:O}");
            sb.AppendLine();
            sb.AppendLine($"Base directory: `{aggregate.BaseDirectory}`");
            sb.AppendLine();
            sb.AppendLine($"Comparison runs aggregated: {aggregate.ComparisonCount}");
            sb.AppendLine();
            sb.AppendLine("## Metrics (averages over all comparison runs)");
            sb.AppendLine();
            sb.AppendLine("| Metric | AvgBaseline | AvgFirst | AvgFirst+Delta | AvgΔFirst-BL | AvgΔFirst+Delta-BL |");
            sb.AppendLine("|--------|-------------|----------|----------------|-------------|--------------------|");

            foreach (var row in aggregate.Metrics)
            {
                sb.AppendLine(
                    $"| {row.Metric} | " +
                    $"{row.AverageBaseline:F3} | " +
                    $"{row.AverageFirst:F3} | " +
                    $"{row.AverageFirstPlusDelta:F3} | " +
                    $"{row.AverageDeltaFirstVsBaseline:+0.000;-0.000;0.000} | " +
                    $"{row.AverageDeltaFirstPlusDeltaVsBaseline:+0.000;-0.000;0.000} |");
            }

            sb.AppendLine();
            return sb.ToString();
        }
    }
}
