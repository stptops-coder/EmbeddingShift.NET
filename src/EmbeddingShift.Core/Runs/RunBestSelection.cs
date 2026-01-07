using System;
using System.Linq;

namespace EmbeddingShift.Core.Runs
{
    public static class RunBestSelection
    {
        public sealed record BestRun(
            RunArtifactDiscovery.DiscoveredRun Run,
            string MetricKey,
            double Score);

        public static BestRun? SelectBest(
            string metricKey,
            System.Collections.Generic.IReadOnlyList<RunArtifactDiscovery.DiscoveredRun> discovered)
        {
            if (string.IsNullOrWhiteSpace(metricKey))
                throw new ArgumentException("Metric key must not be null/empty.", nameof(metricKey));

            if (discovered is null || discovered.Count == 0)
                return null;

            var best = discovered
                .Select(r =>
                {
                    var has = RunArtifactDiscovery.TryGetMetric(r.Artifact, metricKey, out var score);
                    return new { Run = r, Has = has, Score = has ? score : double.NegativeInfinity };
                })
                .Where(x => x.Has)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Run.Artifact.FinishedUtc)
                .ThenByDescending(x => x.Run.Artifact.RunId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (best is null)
                return null;

            return new BestRun(best.Run, metricKey, best.Score);
        }
    }
}
