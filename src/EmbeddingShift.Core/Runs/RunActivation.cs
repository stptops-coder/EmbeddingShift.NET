using System;
using System.Globalization;
using System.IO;
using System.Linq;
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
            => Promote(runsRoot, metricKey, pickRank: null, pickRunId: null);

        /// <summary>
        /// Promotes the selected run to be the active pointer for the given metric.
        /// Selection priority:
        /// - If pickRunId is provided, that exact RunId is used (must exist and contain the metric).
        /// - Else if pickRank is provided, the N-th run by score (1-based) is used.
        /// - Else the best run by score is used.
        /// </summary>
        public static PromoteResult Promote(string runsRoot, string metricKey, int? pickRank, string? pickRunId)
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

            var candidates = discovered
                .Select(r =>
                {
                    if (!RunArtifactDiscovery.TryGetMetric(r.Artifact, metricKey, out var score))
                        return null;

                    return new Candidate(r, score);
                })
                .Where(x => x is not null)
                .Cast<Candidate>()
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Run.Artifact.FinishedUtc)
                .ThenByDescending(x => x.Run.Artifact.RunId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidates.Count == 0)
                throw new InvalidOperationException($"No runs contained metric '{metricKey}' under: {runsRoot}");

            var chosen = SelectCandidate(candidates, pickRank, pickRunId);

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
                WorkflowName: chosen.Run.Artifact.WorkflowName,
                RunId: chosen.Run.Artifact.RunId,
                Score: chosen.Score,
                RunDirectory: chosen.Run.RunDirectory,
                RunJsonPath: chosen.Run.RunJsonPath);

            var jsonOut = JsonSerializer.Serialize(
                pointer,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });

            File.WriteAllText(activePath, jsonOut, new UTF8Encoding(false));

            return new PromoteResult(pointer, activePath, archivedTo);
        }

        private sealed record Candidate(RunArtifactDiscovery.DiscoveredRun Run, double Score);

        private static Candidate SelectCandidate(
            System.Collections.Generic.IReadOnlyList<Candidate> candidates,
            int? pickRank,
            string? pickRunId)
        {
            if (!string.IsNullOrWhiteSpace(pickRunId))
            {
                var match = candidates
                    .FirstOrDefault(x => string.Equals(x.Run.Artifact.RunId, pickRunId, StringComparison.OrdinalIgnoreCase));

                if (match is null)
                    throw new InvalidOperationException($"No run with RunId '{pickRunId}' contained metric under the runs root.");

                return match;
            }

            if (pickRank.HasValue)
            {
                var n = pickRank.Value;
                if (n <= 0 || n > candidates.Count)
                    throw new InvalidOperationException($"Rank '{n}' is out of range. Available ranks: 1..{candidates.Count}.");

                return candidates[n - 1];
            }

            return candidates[0];
        }

        public sealed record RollbackResult(
            ActiveRunPointer Pointer,
            string ActivePath,
            string RestoredFromHistoryPath,
            string? CurrentActiveArchivedTo);

        public static RollbackResult RollbackLatest(string runsRoot, string metricKey)
        {
            if (string.IsNullOrWhiteSpace(runsRoot))
                throw new ArgumentException("Runs root must not be null/empty.", nameof(runsRoot));

            if (!Directory.Exists(runsRoot))
                throw new DirectoryNotFoundException($"Runs root not found: {runsRoot}");

            if (string.IsNullOrWhiteSpace(metricKey))
                throw new ArgumentException("Metric key must not be null/empty.", nameof(metricKey));

            var activePath = GetActivePath(runsRoot, metricKey);
            var activeDir = Path.Combine(runsRoot, "_active");
            var historyDir = Path.Combine(activeDir, "history");

            if (!Directory.Exists(historyDir))
                throw new InvalidOperationException($"No history directory found: {historyDir}");

            var safeMetric = SanitizeFileName(metricKey);
            var pattern = $"active_{safeMetric}_*.json";

            var latest = Directory.EnumerateFiles(historyDir, pattern, SearchOption.TopDirectoryOnly)
                .Select(p => new { Path = p, LastWriteUtc = File.GetLastWriteTimeUtc(p) })
                .OrderByDescending(x => x.LastWriteUtc)
                .FirstOrDefault();

            if (latest is null)
                throw new InvalidOperationException($"No history entries found for metric '{metricKey}' under: {historyDir}");

            // Archive current active pointer (if any) before overwriting it.
            string? archivedCurrent = null;
            if (File.Exists(activePath))
            {
                var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
                archivedCurrent = Path.Combine(historyDir, $"active_{safeMetric}_preRollback_{stamp}.json");
                File.Copy(activePath, archivedCurrent, overwrite: false);
            }

            // Restore latest history entry to active.
            File.Copy(latest.Path, activePath, overwrite: true);

            // Load restored pointer for return value.
            if (!TryLoadActive(runsRoot, metricKey, out var pointer) || pointer is null)
                throw new InvalidOperationException($"Rollback wrote active pointer, but it could not be loaded: {activePath}");

            return new RollbackResult(pointer, activePath, latest.Path, archivedCurrent);
        }

        public sealed record HistoryEntry(
            string Path,
            DateTime LastWriteUtc,
            bool IsPreRollback,
            ActiveRunPointer? Pointer);

        public static System.Collections.Generic.IReadOnlyList<HistoryEntry> ListHistory(
            string runsRoot,
            string metricKey,
            int maxItems = 20,
            bool includePreRollback = true)
        {
            if (string.IsNullOrWhiteSpace(runsRoot))
                throw new ArgumentException("Runs root must not be null/empty.", nameof(runsRoot));

            if (!Directory.Exists(runsRoot))
                throw new DirectoryNotFoundException($"Runs root not found: {runsRoot}");

            if (string.IsNullOrWhiteSpace(metricKey))
                throw new ArgumentException("Metric key must not be null/empty.", nameof(metricKey));

            if (maxItems <= 0) maxItems = 20;

            var activeDir = Path.Combine(runsRoot, "_active");
            var historyDir = Path.Combine(activeDir, "history");
            if (!Directory.Exists(historyDir))
                return Array.Empty<HistoryEntry>();

            var safeMetric = SanitizeFileName(metricKey);
            var pattern = $"active_{safeMetric}_*.json";

            var files = Directory.EnumerateFiles(historyDir, pattern, SearchOption.TopDirectoryOnly);

            if (!includePreRollback)
            {
                files = files.Where(p =>
                    !Path.GetFileName(p).Contains("_preRollback_", StringComparison.OrdinalIgnoreCase));
            }

            var list = files
                .Select(p =>
                {
                    var lastWriteUtc = File.GetLastWriteTimeUtc(p);
                    var isPreRollback = Path.GetFileName(p).Contains("_preRollback_", StringComparison.OrdinalIgnoreCase);

                    ActiveRunPointer? pointer = null;
                    try
                    {
                        var json = File.ReadAllText(p);
                        pointer = JsonSerializer.Deserialize<ActiveRunPointer>(
                            json,
                            new JsonSerializerOptions(JsonSerializerDefaults.Web)
                            {
                                PropertyNameCaseInsensitive = true
                            });
                    }
                    catch
                    {
                        pointer = null;
                    }

                    return new HistoryEntry(p, lastWriteUtc, isPreRollback, pointer);
                })
                .OrderByDescending(x => x.LastWriteUtc)
                .ThenByDescending(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .Take(maxItems)
                .ToArray();

            return list;
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
