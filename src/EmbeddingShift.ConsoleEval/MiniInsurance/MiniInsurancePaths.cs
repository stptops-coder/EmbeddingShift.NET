using System;
using System.IO;

namespace EmbeddingShift.ConsoleEval.MiniInsurance;

/// <summary>
/// Provides a stable base path for all Mini-Insurance artifacts
/// outside of bin/Debug.
/// </summary>
internal static class MiniInsurancePaths
{
    private const string DomainFolderName = "mini-insurance";
    private const string LocalFolderName = "local";

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
        // AppContext.BaseDirectory = ...\src\EmbeddingShift.ConsoleEval\bin\Debug\net8.0\
        // Go up three levels to reach the ConsoleEval project root.
        var consoleEvalProjectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

        var domainRoot = Path.Combine(
            consoleEvalProjectRoot,
            LocalFolderName,
            DomainFolderName);

        Directory.CreateDirectory(domainRoot);
        return domainRoot;
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
