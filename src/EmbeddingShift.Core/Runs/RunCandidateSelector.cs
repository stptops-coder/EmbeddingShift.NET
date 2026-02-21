using System;
using System.IO;

namespace EmbeddingShift.Core.Runs
{
    /// <summary>
    /// Selects a best run candidate for a metric from one or more sources.
    /// By default only the "normal" runs under <paramref name="runsRoot"/> are considered
    /// (repo runs under runsRoot\_repo are intentionally excluded by RunArtifactDiscovery).
    /// </summary>
    public static class RunCandidateSelector
    {
        public sealed record CandidateSelection(
            RunArtifactDiscovery.DiscoveredRun Run,
            string MetricKey,
            double Score,
            int TotalRunsFound);

        /// <summary>
        /// Selects the best run for <paramref name="metricKey"/> from:
        /// - Normal runs under <paramref name="runsRoot"/> (excluding runsRoot\_repo),
        /// - Optionally, MiniInsurance-PosNeg repo runs under runsRoot\_repo\MiniInsurance-PosNeg.
        /// </summary>
        public static CandidateSelection SelectBestCandidate(string runsRoot, string metricKey, bool includeRepoPosNeg)
        {
            if (string.IsNullOrWhiteSpace(runsRoot))
                throw new ArgumentException("Runs root must not be null/empty.", nameof(runsRoot));

            if (string.IsNullOrWhiteSpace(metricKey))
                throw new ArgumentException("Metric key must not be null/empty.", nameof(metricKey));

            var discovered = RunArtifactDiscovery.Discover(runsRoot);

            RunBestSelection.BestRun? bestNormal = RunBestSelection.SelectBest(metricKey, discovered);

            RunBestSelection.BestRun? bestRepoPosNeg = null;
            var repoCount = 0;

            if (includeRepoPosNeg)
            {
                var repoRoot = Path.Combine(runsRoot, "_repo", "MiniInsurance-PosNeg");
                if (Directory.Exists(repoRoot))
                {
                    var repoRuns = RunArtifactDiscovery.Discover(repoRoot);
                    repoCount = repoRuns.Count;
                    bestRepoPosNeg = RunBestSelection.SelectBest(metricKey, repoRuns);
                }
            }

            var total = discovered.Count + repoCount;

            // Compatibility with legacy behavior:
            // - Without repo inclusion, an empty discovered set is an error.
            // - With repo inclusion, allow repo-only candidates.
            if (!includeRepoPosNeg && discovered.Count == 0)
                throw new InvalidOperationException($"No run.json found under: {runsRoot}");

            if (includeRepoPosNeg && total == 0)
                throw new InvalidOperationException($"No run.json found under: {runsRoot}");

            var best = ChooseBest(bestNormal, bestRepoPosNeg);

            if (best is null)
                throw new InvalidOperationException($"No runs contained metric '{metricKey}' under: {runsRoot}");

            return new CandidateSelection(best.Run, metricKey, best.Score, total);
        }

        private static RunBestSelection.BestRun? ChooseBest(
            RunBestSelection.BestRun? a,
            RunBestSelection.BestRun? b)
        {
            if (a is null) return b;
            if (b is null) return a;

            // Primary: score
            if (a.Score > b.Score) return a;
            if (b.Score > a.Score) return b;

            // Tie-break: finished time, then runId (stable)
            var aFinished = a.Run.Artifact.FinishedUtc;
            var bFinished = b.Run.Artifact.FinishedUtc;

            if (aFinished > bFinished) return a;
            if (bFinished > aFinished) return b;

            return string.Compare(a.Run.Artifact.RunId, b.Run.Artifact.RunId, StringComparison.OrdinalIgnoreCase) >= 0 ? a : b;
        }
    }
}
