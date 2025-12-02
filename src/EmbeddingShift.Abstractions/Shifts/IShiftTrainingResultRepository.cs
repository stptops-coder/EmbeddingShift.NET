using System;

namespace EmbeddingShift.Abstractions.Shifts;

/// <summary>
/// Represents the result of a shift training run for a given workflow.
/// This is a generic shape that can be used across domains (insurance,
/// medical, finance, ...).
/// </summary>
public sealed record ShiftTrainingResult
{
    /// <summary>
    /// Logical workflow identifier, e.g. "mini-insurance-first-delta".
    /// </summary>
    public string WorkflowName { get; init; } = string.Empty;

    /// <summary>
    /// UTC timestamp when this training result was created.
    /// </summary>
    public DateTime CreatedUtc { get; init; }

    /// <summary>
    /// Logical base directory or root for this workflow's results.
    /// For file-based repositories this typically points to the
    /// results root (e.g. ".../results/insurance").
    /// </summary>
    public string BaseDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Number of comparison runs that contributed to this training result.
    /// </summary>
    public int ComparisonRuns { get; init; }

    /// <summary>
    /// Aggregated improvement of the "First" shift vs the baseline.
    /// </summary>
    public double ImprovementFirst { get; init; }

    /// <summary>
    /// Aggregated improvement of "First+Delta" vs the baseline.
    /// </summary>
    public double ImprovementFirstPlusDelta { get; init; }

    /// <summary>
    /// Additional improvement of "First+Delta" vs "First" alone.
    /// </summary>
    public double DeltaImprovement { get; init; }

    /// <summary>
    /// Learned delta vector in embedding space. Length should match the
    /// embedding dimension (e.g. 1536 for OpenAI text-embeddings).
    /// </summary>
    public float[] DeltaVector { get; init; } = Array.Empty<float>();

    public string ScopeId { get; init; } = "default";

}

/// <summary>
/// Repository abstraction for persisting and retrieving shift training
/// results for a given workflow. This is intentionally small and can be
/// implemented using the file system, a database or any other backend.
/// </summary>
public interface IShiftTrainingResultRepository
{
    /// <summary>
    /// Persists the specified training result.
    /// Implementations may choose their own layout (e.g. directory
    /// structure, table schema) but should preserve the record shape.
    /// </summary>
    /// <param name="result">The training result to save.</param>
    void Save(ShiftTrainingResult result);

    /// <summary>
    /// Loads the latest training result for the given workflow name,
    /// or null if none is available.
    /// </summary>
    /// <param name="workflowName">
    /// Logical workflow identifier, e.g. "mini-insurance-first-delta".
    /// </param>
    /// <returns>The latest training result or null.</returns>
    ShiftTrainingResult? LoadLatest(string workflowName);
}
