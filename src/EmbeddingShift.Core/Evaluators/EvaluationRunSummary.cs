using System;
using System.Collections.Generic;

namespace EmbeddingShift.Core.Evaluators
{
    /// <summary>
    /// Structured summary of an evaluation run.
    /// Intended for UI and automation scenarios (in addition to console logging).
    /// </summary>
    public sealed record EvaluationRunSummary(
        Guid RunId,
        string Kind,
        string Dataset,
        DateTime StartedAtUtc,
        DateTime CompletedAtUtc,
        string ResultsPath,
        IReadOnlyDictionary<string, double> Metrics);
}
