using System.Collections.Generic;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// Minimal per-query evaluation breakdown. Intended as a stable artifact for downstream experiments
    /// (e.g., segmentation) without relying on console output parsing.
    /// </summary>
    public sealed record PerQueryEval(
        string QueryId,
        string RelevantDocId,
        int Rank,
        double Ap1,
        double Ndcg3,
        string? TopDocId,
        double TopScore);

    /// <summary>
    /// Optional capability interface: workflows that can expose per-query metrics.
    /// </summary>
    public interface IPerQueryEvalProvider
    {
        IReadOnlyList<PerQueryEval> PerQuery { get; }
    }
}
