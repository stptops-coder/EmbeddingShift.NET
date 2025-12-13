namespace EmbeddingShift.ConsoleEval.Domains;

using System;
using System.Linq;
using System.Threading.Tasks;
using EmbeddingShift.ConsoleEval.Commands;
using EmbeddingShift.ConsoleEval.Inspector;
using EmbeddingShift.ConsoleEval.MiniInsurance;
using EmbeddingShift.Core.Infrastructure;

/// <summary>
/// Domain pack: Mini-Insurance.
/// This remains a reference "domain pack" while the engine stays domain-neutral.
/// </summary>
internal sealed class MiniInsuranceDomainPack : IDomainPack
{
    public string DomainId => "mini-insurance";
    public string DisplayName => "Mini-Insurance (reference domain pack)";
    public string ResultsDomainKey => "insurance";

    public void PrintHelp(Action<string> log)
    {
        log("Mini-Insurance domain pack");
        log("Usage:");
        log("  domain mini-insurance pipeline [--no-learned]");
        log("  domain mini-insurance training-list");
        log("  domain mini-insurance training-inspect");
        log("  domain mini-insurance shift-training-inspect");
        log("  domain mini-insurance shift-training-history [maxItems]");
        log("  domain mini-insurance shift-training-best");
    }

    public async Task<int> ExecuteAsync(string[] args, Action<string> log)
    {
        var sub = args.FirstOrDefault()?.ToLowerInvariant() ?? "help";

        switch (sub)
        {
            case "help":
            case "--help":
            case "-h":
                PrintHelp(log);
                return 0;

            case "pipeline":
            case "run":
                {
                    var includeLearned =
                        !args.Any(a => string.Equals(a, "--no-learned", StringComparison.OrdinalIgnoreCase));

                    var pipeline = new MiniInsuranceFirstDeltaPipeline(log);
                    await pipeline.RunAsync(includeLearnedDelta: includeLearned);
                    return 0;
                }

            case "training-list":
                await MiniInsuranceTrainingListCommand.RunAsync(args.Skip(1).ToArray());
                return 0;

            case "training-inspect":
                await MiniInsuranceTrainingInspectCommand.RunAsync(args.Skip(1).ToArray());
                return 0;

            case "shift-training-inspect":
                {
                    var root = DirectoryLayout.ResolveResultsRoot(ResultsDomainKey);
                    ShiftTrainingResultInspector.PrintLatest(
                        workflowName: "mini-insurance-first-delta",
                        rootDirectory: root);
                    return 0;
                }

            case "shift-training-history":
                {
                    var root = DirectoryLayout.ResolveResultsRoot(ResultsDomainKey);

                    var maxItems = 20;
                    if (args.Length >= 2 && int.TryParse(args[1], out var parsed) && parsed > 0)
                    {
                        maxItems = parsed;
                    }

                    ShiftTrainingResultInspector.PrintHistory(
                        workflowName: "mini-insurance-first-delta",
                        rootDirectory: root,
                        maxItems: maxItems);

                    return 0;
                }

            case "shift-training-best":
                {
                    var root = DirectoryLayout.ResolveResultsRoot(ResultsDomainKey);
                    ShiftTrainingResultInspector.PrintBest(
                        workflowName: "mini-insurance-first-delta",
                        rootDirectory: root);
                    return 0;
                }

            default:
                log($"Unknown subcommand '{sub}'.");
                PrintHelp(log);
                return 1;
        }
    }
}
