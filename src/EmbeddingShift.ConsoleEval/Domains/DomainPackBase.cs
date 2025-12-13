namespace EmbeddingShift.ConsoleEval.Domains;

using System;
using System.Linq;
using System.Threading.Tasks;
using EmbeddingShift.ConsoleEval.Commands;

/// <summary>
/// Base class for ConsoleEval domain packs.
/// Keeps common subcommands (help + shift-training inspection) in one place,
/// so new domain packs only implement their domain-specific commands.
/// </summary>
internal abstract class DomainPackBase : IDomainPack
{
    public abstract string DomainId { get; }
    public abstract string DisplayName { get; }
    public abstract string ResultsDomainKey { get; }

    protected abstract string DefaultWorkflowName { get; }

    public void PrintHelp(Action<string> log) => PrintDomainHelp(log);

    protected abstract void PrintDomainHelp(Action<string> log);

    public async Task<int> ExecuteAsync(string[] args, Action<string> log)
    {
        var sub = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";

        switch (sub)
        {
            case "help":
            case "--help":
            case "-h":
                PrintDomainHelp(log);
                return 0;

            case "shift-training-inspect":
                {
                    var workflowName = args.Length >= 2 ? args[1] : DefaultWorkflowName;
                    await ShiftTrainingInspectCommand.RunAsync(new[] { workflowName, ResultsDomainKey });
                    return 0;
                }

            case "shift-training-history":
                {
                    var workflowName = args.Length >= 2 ? args[1] : DefaultWorkflowName;
                    var maxItems = 20;

                    if (args.Length >= 3 && int.TryParse(args[2], out var parsed) && parsed > 0)
                        maxItems = parsed;

                    await ShiftTrainingHistoryCommand.RunAsync(
                        new[] { workflowName, maxItems.ToString(), ResultsDomainKey });

                    return 0;
                }

            case "shift-training-best":
                {
                    var workflowName = args.Length >= 2 ? args[1] : DefaultWorkflowName;
                    await ShiftTrainingBestCommand.RunAsync(new[] { workflowName, ResultsDomainKey });
                    return 0;
                }

            default:
                return await ExecuteDomainCommandAsync(sub, args, log);
        }
    }

    protected abstract Task<int> ExecuteDomainCommandAsync(string sub, string[] args, Action<string> log);
}
