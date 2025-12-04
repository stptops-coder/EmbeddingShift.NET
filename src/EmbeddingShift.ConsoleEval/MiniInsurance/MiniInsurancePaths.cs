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
}
