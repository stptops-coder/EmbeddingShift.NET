using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using EmbeddingShift.Core.Stats;

namespace EmbeddingShift.Core.Runs
{
    /// <summary>
    /// Discovers persisted run artifacts (run.json) under a given runs root.
    /// This is intentionally domain-neutral and supports ad-hoc comparisons.
    /// </summary>
    public static class RunArtifactDiscovery
    {
        public sealed record DiscoveredRun(
            WorkflowRunArtifact Artifact,
            string RunJsonPath,
            string RunDirectory);

        public static IReadOnlyList<DiscoveredRun> Discover(string runsRoot)
        {
            if (string.IsNullOrWhiteSpace(runsRoot))
                throw new ArgumentException("Runs root must not be null/empty.", nameof(runsRoot));

            if (!Directory.Exists(runsRoot))
                return Array.Empty<DiscoveredRun>();

            var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true
            };

            var list = new List<DiscoveredRun>();

            foreach (var path in Directory.EnumerateFiles(runsRoot, "run.json", SearchOption.AllDirectories))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var artifact = JsonSerializer.Deserialize<WorkflowRunArtifact>(json, opts);
                    if (artifact is null) continue;

                    var dir = Path.GetDirectoryName(path) ?? runsRoot;
                    list.Add(new DiscoveredRun(artifact, path, dir));
                }
                catch
                {
                    // Intentionally ignore malformed artifacts; comparison should be resilient.
                }
            }

            return list
                .OrderByDescending(r => r.Artifact.FinishedUtc)
                .ToList();
        }

        public static bool TryGetMetric(WorkflowRunArtifact artifact, string metricKey, out double value)
        {
            value = default;

            if (artifact is null) return false;
            if (string.IsNullOrWhiteSpace(metricKey)) return false;
            if (artifact.Metrics is null) return false;

            foreach (var kv in artifact.Metrics)
            {
                if (string.Equals(kv.Key, metricKey, StringComparison.OrdinalIgnoreCase))
                {
                    value = kv.Value;
                    return true;
                }
            }

            return false;
        }
    }
}
