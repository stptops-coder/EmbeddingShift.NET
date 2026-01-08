using System;

namespace EmbeddingShift.Core.Runs
{
    /// <summary>
    /// Decision artifact for promoting (or keeping) the active run pointer.
    /// This is a thin, deterministic comparison between:
    /// - the best discovered run under a runs root (by a metric), and
    /// - the current active pointer (if any).
    /// </summary>
    public sealed record RunPromotionDecision(
        string MetricKey,
        double Epsilon,
        DateTimeOffset CreatedUtc,
        string RunsRoot,
        int TotalRunsFound,
        RunPromotionDecisionEntry Candidate,
        RunPromotionDecisionEntry? Active,
        RunPromotionDecisionAction Action,
        double Delta,
        string Reason);

    public sealed record RunPromotionDecisionEntry(
        string WorkflowName,
        string RunId,
        double Score,
        string RunDirectory,
        string RunJsonPath);

    public enum RunPromotionDecisionAction
    {
        Promote,
        KeepActive
    }
}
