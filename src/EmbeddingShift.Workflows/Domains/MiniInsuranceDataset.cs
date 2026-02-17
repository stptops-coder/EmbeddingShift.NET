namespace EmbeddingShift.Workflows.Domains;

using EmbeddingShift.Core.Infrastructure;
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
    private const string DatasetRootEnvVarPrimary = "EMBEDDINGSHIFT_DATASET_ROOT";
    private const string DatasetRootEnvVarLegacy = "EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT";

    /// <summary>
    /// Returns the root directory of the Mini-Insurance sample dataset
    /// (repo-root/samples/insurance).
    /// </summary>
    public static string ResolveDatasetRoot()
    {
        var repoRoot = RepositoryLayout.ResolveRepoRoot();

        // Optional override (absolute or repo-relative), e.g.:
        //   results/insurance/datasets/<name>/stage-00
        var overrideValue = Environment.GetEnvironmentVariable(DatasetRootEnvVarPrimary);
        if (string.IsNullOrWhiteSpace(overrideValue))
        {
            overrideValue = Environment.GetEnvironmentVariable(DatasetRootEnvVarLegacy);
        }
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            var trimmed = overrideValue.Trim();
            return Path.IsPathRooted(trimmed)
                ? Path.GetFullPath(trimmed)
                : Path.GetFullPath(Path.Combine(repoRoot, trimmed));
        }

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
