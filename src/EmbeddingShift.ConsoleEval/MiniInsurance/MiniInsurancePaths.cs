using System;
using System.IO;
using EmbeddingShift.Core.Infrastructure;

namespace EmbeddingShift.ConsoleEval.MiniInsurance;

/// <summary>
/// Provides stable base paths for all Mini-Insurance artifacts under:
///   <repo-root>/results/insurance
/// This keeps artifacts out of bin/Debug and makes CLI tooling consistent.
/// </summary>
internal static class MiniInsurancePaths
{
    // Keep this aligned with MiniInsuranceDomainPack.ResultsDomainKey
    // and with the domain dataset folder name under data/domains.
    private const string ResultsDomainKey = "insurance";

    private const string RunsFolderName = "runs";
    private const string TrainingFolderName = "training";
    private const string AggregatesFolderName = "aggregates";
    private const string InspectFolderName = "inspect";
    private const string DatasetsFolderName = "datasets";


    /// <summary>
    /// Returns the stable Mini-Insurance domain root directory and
    /// ensures that it exists.
    ///
    /// Example (from ConsoleEval project root):
    ///   src/EmbeddingShift.ConsoleEval/local/mini-insurance
    /// </summary>
    public static string GetDomainRoot()
    {
        // Tenant scoping is applied centrally in DirectoryLayout.ResolveResultsRoot(...).
        return DirectoryLayout.ResolveResultsRoot(ResultsDomainKey);
    }


    private static string? GetTenantKeyOrNull()
    {
        var raw = Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_TENANT");
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return SanitizeFolderKey(raw);
    }

    private static string SanitizeFolderKey(string value)
    {
        // Keep it predictable and filesystem-safe:
        // - lower invariant
        // - allow [a-z0-9-_]
        // - everything else -> '-'
        // - trim leading/trailing '-'
        var s = value.Trim().ToLowerInvariant();
        if (s.Length == 0)
            return "tenant";

        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            var ok =
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') ||
                c == '-' ||
                c == '_';
            if (!ok)
                chars[i] = '-';
        }

        var cleaned = new string(chars).Trim('-');
        return cleaned.Length == 0 ? "tenant" : cleaned;
    }


    /// <summary>
    /// Root for all run artifacts (raw comparison runs etc.).
    /// </summary>
    public static string GetRunsRoot()
        => EnsureSubFolder(RunsFolderName);

    /// <summary>
    /// Root for all training artifacts (training data, learned shifts etc.).
    /// </summary>
    public static string GetTrainingRoot()
        => EnsureSubFolder(TrainingFolderName);

    /// <summary>
    /// Root for all aggregate artifacts (aggregated metrics, summaries etc.).
    /// </summary>
    public static string GetAggregatesRoot()
        => EnsureSubFolder(AggregatesFolderName);

    /// <summary>
    /// Root for all inspection artifacts (dumps used by inspection tools).
    /// </summary>
    public static string GetInspectRoot()
        => EnsureSubFolder(InspectFolderName);

    /// <summary>
    /// Root for generated datasets (staged corpora) under results/insurance/datasets.
    /// </summary>
    public static string GetDatasetsRoot()
        => EnsureSubFolder(DatasetsFolderName);

    /// <summary>
    /// Root directory for a named dataset (contains stage-00, stage-01, ...).
    /// </summary>
    public static string GetDatasetRoot(string datasetName)
    {
        if (string.IsNullOrWhiteSpace(datasetName))
            throw new ArgumentException("Dataset name must not be empty.", nameof(datasetName));

        var root = Path.Combine(GetDatasetsRoot(), datasetName.Trim());
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// Root directory for a specific stage within a dataset.
    /// </summary>
    public static string GetStageRoot(string datasetName, int stageIndex)
    {
        if (stageIndex < 0) throw new ArgumentOutOfRangeException(nameof(stageIndex));
        var stage = $"stage-{stageIndex:00}";
        var root = Path.Combine(GetDatasetRoot(datasetName), stage);
        Directory.CreateDirectory(root);
        return root;
    }

    private static string EnsureSubFolder(string folderName)
    {
        var root = GetDomainRoot();
        var path = Path.Combine(root, folderName);
        Directory.CreateDirectory(path);
        return path;
    }
}
