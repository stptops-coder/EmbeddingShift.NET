namespace EmbeddingShift.Ingest;

using System;

/// <summary>
/// Placeholder for a future ingestion pipeline.
///
/// Intention:
/// - Current EmbeddingShift experiments (Mini-Insurance, Adaptive workflow)
///   operate purely on file-based artifacts.
/// - Once a real database or SQL export is required, this pipeline can be
///   implemented to transform domain text artifacts into INSERT scripts or
///   other storage-ready formats.
/// </summary>
public sealed class IngestPipeline
{
    /// <summary>
    /// Runs the ingestion pipeline for a given input directory.
    /// For now this method is intentionally not implemented, because
    /// the project stays file-based until a concrete DB/storage target
    /// is requested.
    /// </summary>
    /// <param name="inputDir">Directory containing the raw domain artifacts.</param>
    /// <param name="outSqlDir">
    /// Target directory for storage-ready outputs (e.g. SQL scripts) in a future implementation.
    /// </param>
    public void Run(string inputDir, string outSqlDir)
    {
        throw new NotImplementedException(
            "IngestPipeline is intentionally not implemented yet. " +
            "Current EmbeddingShift experiments use file-based artifacts only. " +
            "Add a concrete implementation here once a real DB or SQL export is required.");
    }
}
