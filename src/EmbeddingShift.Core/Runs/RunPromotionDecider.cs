using System;

namespace EmbeddingShift.Core.Runs
{
    public static class RunPromotionDecider
    {
        /// <summary>
        /// Produces a promotion decision against the current active pointer (if present).
        /// Rules:
        /// - If no active pointer exists: PROMOTE.
        /// - If the best candidate equals the active run: KEEP_ACTIVE.
        /// - Else: PROMOTE iff (candidate.Score - active.Score) > epsilon.
        /// </summary>
        public static RunPromotionDecision Decide(string runsRoot, string metricKey, double epsilon = 1e-6)
            => Decide(runsRoot, metricKey, profileKey: null, epsilon: epsilon, includeRepoPosNeg: false);

        /// <summary>
        /// Same as <see cref="Decide(string,string,double)"/>, but scopes the active pointer to a profile key.
        /// If profileKey is null/empty, the legacy (non-profile) active pointer is used.
        /// </summary>
        public static RunPromotionDecision Decide(string runsRoot, string metricKey, string? profileKey, double epsilon = 1e-6)
            => Decide(runsRoot, metricKey, profileKey, epsilon, includeRepoPosNeg: false);

        /// <summary>
        /// Same as <see cref="Decide(string,string,string?,double)"/>, but can optionally consider
        /// MiniInsurance-PosNeg repo runs under runsRoot\_repo\MiniInsurance-PosNeg as an additional candidate source.
        /// </summary>
        public static RunPromotionDecision Decide(string runsRoot, string metricKey, string? profileKey, double epsilon, bool includeRepoPosNeg)
        {
            if (string.IsNullOrWhiteSpace(runsRoot))
                throw new ArgumentException("Runs root must not be null/empty.", nameof(runsRoot));

            if (string.IsNullOrWhiteSpace(metricKey))
                throw new ArgumentException("Metric key must not be null/empty.", nameof(metricKey));

            if (epsilon < 0)
                throw new ArgumentOutOfRangeException(nameof(epsilon), "Epsilon must be >= 0.");

            var selection = RunCandidateSelector.SelectBestCandidate(runsRoot, metricKey, includeRepoPosNeg);

            var best = selection.Run;
            var candidateEntry = new RunPromotionDecisionEntry(
                WorkflowName: best.Artifact.WorkflowName,
                RunId: best.Artifact.RunId,
                Score: selection.Score,
                RunDirectory: best.RunDirectory,
                RunJsonPath: best.RunJsonPath);

            if (!RunActivation.TryLoadActive(runsRoot, metricKey, profileKey, out var activePointer) || activePointer is null)
            {
                return new RunPromotionDecision(
                    MetricKey: metricKey,
                    Epsilon: epsilon,
                    CreatedUtc: DateTimeOffset.UtcNow,
                    RunsRoot: runsRoot,
                    TotalRunsFound: selection.TotalRunsFound,
                    Candidate: candidateEntry,
                    Active: null,
                    Action: RunPromotionDecisionAction.Promote,
                    Delta: 0.0,
                    Reason: "No active pointer exists for this metric; initial activation recommended.");
            }

            var activeEntry = new RunPromotionDecisionEntry(
                WorkflowName: activePointer.WorkflowName,
                RunId: activePointer.RunId,
                Score: activePointer.Score,
                RunDirectory: activePointer.RunDirectory,
                RunJsonPath: activePointer.RunJsonPath);

            // Same run already active
            if (string.Equals(candidateEntry.RunDirectory, activeEntry.RunDirectory, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidateEntry.RunJsonPath, activeEntry.RunJsonPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidateEntry.RunId, activeEntry.RunId, StringComparison.OrdinalIgnoreCase))
            {
                return new RunPromotionDecision(
                    MetricKey: metricKey,
                    Epsilon: epsilon,
                    CreatedUtc: DateTimeOffset.UtcNow,
                    RunsRoot: runsRoot,
                    TotalRunsFound: selection.TotalRunsFound,
                    Candidate: candidateEntry,
                    Active: activeEntry,
                    Action: RunPromotionDecisionAction.KeepActive,
                    Delta: 0,
                    Reason: "The best discovered run is already active.");
            }

            var delta = candidateEntry.Score - activeEntry.Score;

            var action = delta > epsilon
                ? RunPromotionDecisionAction.Promote
                : RunPromotionDecisionAction.KeepActive;

            var reason = action == RunPromotionDecisionAction.Promote
                ? $"Candidate improves '{metricKey}' by {delta:0.000000} (epsilon={epsilon:0.000000})."
                : $"Candidate does not improve '{metricKey}' beyond epsilon (delta={delta:0.000000}, epsilon={epsilon:0.000000}).";

            return new RunPromotionDecision(
                MetricKey: metricKey,
                Epsilon: epsilon,
                CreatedUtc: DateTimeOffset.UtcNow,
                RunsRoot: runsRoot,
                TotalRunsFound: selection.TotalRunsFound,
                Candidate: candidateEntry,
                Active: activeEntry,
                Action: action,
                Delta: delta,
                Reason: reason);
        }
    }
}
