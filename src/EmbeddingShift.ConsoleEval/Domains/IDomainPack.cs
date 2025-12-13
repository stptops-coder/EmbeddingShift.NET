namespace EmbeddingShift.ConsoleEval.Domains;

using System;
using System.Threading.Tasks;

/// <summary>
/// A small, file-based domain bundle that can expose domain-specific
/// ConsoleEval commands behind a stable entry point.
/// </summary>
internal interface IDomainPack
{
    /// <summary>
    /// Stable identifier used by the CLI (e.g., "mini-insurance").
    /// </summary>
    string DomainId { get; }

    /// <summary>
    /// Human-readable name used in CLI help output.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Folder key for results storage (used by DirectoryLayout.ResolveResultsRoot).
    /// </summary>
    string ResultsDomainKey { get; }

    void PrintHelp(Action<string> log);

    /// <summary>
    /// Executes a domain subcommand. Returns an exit code (0 = success).
    /// </summary>
    Task<int> ExecuteAsync(string[] args, Action<string> log);
}