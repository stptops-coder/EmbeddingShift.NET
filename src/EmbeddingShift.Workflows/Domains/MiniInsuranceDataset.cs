namespace EmbeddingShift.Workflows.Domains;

using System;
using System.IO;

/// <summary>
/// Describes the physical layout of the Mini-Insurance sample dataset.
/// This keeps the "samples/insurance" convention in a single place so that
/// tests, trainers and console tools can share it and future refactors
/// only need to adjust this class.
/// </summary>
public static class MiniInsuranceDataset
{
    /// <summary>
    /// Returns the root directory of the Mini-Insurance sample dataset
    /// (repo-root/samples/insurance).
    /// </summary>
    public static string ResolveDatasetRoot()
    {
        // Start from bin/Debug/net8.0 (ConsoleEval or Tests) and go back to repo root.
        var baseDir = AppContext.BaseDirectory;

        var repoRoot = Path.GetFullPath(
            Path.Combine(baseDir, "..", "..", "..", "..", ".."));

        return Path.Combine(repoRoot, "samples", "insurance");
    }

    /// <summary>
    /// Returns the directory that contains the sample policy documents.
    /// </summary>
    public static string GetPoliciesDirectory()
        => Path.Combine(ResolveDatasetRoot(), "policies");

    /// <summary>
    /// Returns the path to the queries.json file for the Mini-Insurance dataset.
    /// </summary>
    public static string GetQueriesFile()
        => Path.Combine(ResolveDatasetRoot(), "queries", "queries.json");
}
