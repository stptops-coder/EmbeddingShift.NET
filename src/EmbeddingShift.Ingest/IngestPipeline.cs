namespace EmbeddingShift.Ingest;

using System;

/// <summary>
/// Deferred database-export boundary for ingestion.
///
/// The current verified baseline operates on file-based artifacts. This type
/// marks the optional integration point for transforming domain text artifacts
/// into storage-ready outputs when a concrete database or SQL export target is required.
/// </summary>
public sealed class IngestPipeline
{
    /// <summary>
    /// Runs the deferred database-export path for a given input directory.
    /// The current verification baseline remains file-based until a concrete
    /// DB/storage target is selected.
    /// </summary>
    /// <param name="inputDir">Directory containing the raw domain artifacts.</param>
    /// <param name="outSqlDir">
    /// Target directory for storage-ready outputs, such as SQL scripts.
    /// </param>
    public void Run(string inputDir, string outSqlDir)
    {
        throw new NotImplementedException(
            "Database/SQL ingest export is outside the current file-based verification baseline. " +
            "Select a concrete storage target before implementing this boundary.");
    }
}
