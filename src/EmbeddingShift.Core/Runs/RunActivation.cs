using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace EmbeddingShift.Core.Runs
{
    public sealed record ActiveRunPointer(
        string MetricKey,
        DateTimeOffset CreatedUtc,
        string RunsRoot,
        int TotalRunsFound,
        string WorkflowName,
        string RunId,
        double Score,
        string RunDirectory,
        string RunJsonPath);

    public static class RunActivation
    {
        public sealed record PromoteResult(
            ActiveRunPointer Pointer,
            string ActivePath,
            string? PreviousActiveArchivedTo);

        public static bool TryLoadActive(string runsRoot, string metricKey, out ActiveRunPointer? pointer)
        {
            pointer = null;

            if (string.IsNullOrWhiteSpace(runsRoot))
                return false;

            var activePath = GetActivePath(runsRoot, metricKey);
            if (!File.Exists(activePath))
                return false;

            try
            {
                var json = File.ReadAllText(activePath);
                pointer = JsonSerializer.Deserialize<ActiveRunPointer>(
                    json,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });

                return pointer is not null;
            }
            catch
            {
                pointer = null;
                return false;
            }
        }

        public static PromoteResult Promote(string runsRoot, string metricKey)
        {
            if (string.IsNullOrWhiteSpace(runsRoot))
                throw new ArgumentException("Runs root must not be null/empty.", nameof(runsRoot));

            if (!Directory.Exists(runsRoot))
                throw new DirectoryNotFoundException($"Runs root not found: {runsRoot}");

            if (string.IsNullOrWhiteSpace(metricKey))
                throw new ArgumentException("Metric key must not be null/empty.", nameof(metricKey));

            var discovered = RunArtifactDiscovery.Discover(runsRoot);
            if (discovered.Count == 0)
                throw new InvalidOperationException($"No run.json found under: {runsRoot}");

            var best = RunBestSelection.SelectBest(metricKey, discovered);
            if (best is null)
                throw new InvalidOperationException($"No runs contained metric '{metricKey}' under: {runsRoot}");

            var activeDir = Path.Combine(runsRoot, "_active");
            Directory.CreateDirectory(activeDir);

            var activePath = GetActivePath(runsRoot, metricKey);

            // Archive previous active pointer (if any)
            string? archivedTo = null;
            if (File.Exists(activePath))
            {
                var historyDir = Path.Combine(activeDir, "history");
                Directory.CreateDirectory(historyDir);

                var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
                var safeMetric = SanitizeFileName(metricKey);

                archivedTo = Path.Combine(historyDir, $"active_{safeMetric}_{stamp}.json");
                File.Copy(activePath, archivedTo, overwrite: false);
            }

            var pointer = new ActiveRunPointer(
                MetricKey: metricKey,
                CreatedUtc: DateTimeOffset.UtcNow,
                RunsRoot: runsRoot,
                TotalRunsFound: discovered.Count,
                WorkflowName: best.Run.Artifact.WorkflowName,
                RunId: best.Run.Artifact.RunId,
                Score: best.Score,
                RunDirectory: best.Run.RunDirectory,
                RunJsonPath: best.Run.RunJsonPath);

            var jsonOut = JsonSerializer.Serialize(
                pointer,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });

            File.WriteAllText(activePath, jsonOut, new UTF8Encoding(false));

            return new PromoteResult(pointer, activePath, archivedTo);
        }

        private static string GetActivePath(string runsRoot, string metricKey)
        {
            var activeDir = Path.Combine(runsRoot, "_active");
            var safeMetric = SanitizeFileName(metricKey);
            return Path.Combine(activeDir, $"active_{safeMetric}.json");
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);

            foreach (var ch in name)
                sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);

            return sb.ToString();
        }
    }
}
