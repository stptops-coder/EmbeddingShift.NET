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

    /// <summary>
    /// Returns the stable Mini-Insurance domain root directory and
    /// ensures that it exists.
    ///
    /// Example (from ConsoleEval project root):
    ///   src/EmbeddingShift.ConsoleEval/local/mini-insurance
    /// </summary>
    public static string GetDomainRoot()
    {
        // Preferred stable layout:
        //   <repo-root>/results/insurance
        return DirectoryLayout.ResolveResultsRoot(ResultsDomainKey);
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

    private static string EnsureSubFolder(string folderName)
    {
        var root = GetDomainRoot();
        var path = Path.Combine(root, folderName);
        Directory.CreateDirectory(path);
        return path;
    }
}
