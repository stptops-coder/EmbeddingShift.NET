using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace EmbeddingShift.ConsoleEval
{
    /// <summary>
    /// File-system based implementation of IMetricsRepository.
    /// Stores metrics as JSON plus Markdown under a timestamped directory
    /// rooted at the given base directory.
    /// </summary>
    public sealed class FileSystemMetricsRepository : IMetricsRepository
    {
        public string SaveMiniInsuranceFirstDeltaComparison(
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
            var markdown = MiniInsuranceFirstDeltaArtifacts.BuildMarkdown(comparison);
            File.WriteAllText(markdownPath, markdown, encoding);

            return comparisonDir;
        }

        public string SaveMiniInsuranceFirstDeltaAggregate(
            string baseDirectory,
            MiniInsuranceFirstDeltaAggregate aggregate)
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
            var markdown = MiniInsuranceFirstDeltaAggregator.BuildMarkdown(aggregate);
            File.WriteAllText(markdownPath, markdown, encoding);

            return aggregateDir;
        }
    }
}
