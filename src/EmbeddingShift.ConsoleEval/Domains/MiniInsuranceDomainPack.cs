namespace EmbeddingShift.ConsoleEval.Domains;

using System;
using System.Linq;
using System.Threading.Tasks;
using EmbeddingShift.ConsoleEval.Commands;
using EmbeddingShift.ConsoleEval.MiniInsurance;

/// <summary>
/// Domain pack: Mini-Insurance.
/// This remains a reference "domain pack" while the engine stays domain-neutral.
/// </summary>
internal sealed class MiniInsuranceDomainPack : DomainPackBase
{
    public override string DomainId => "mini-insurance";
    public override string DisplayName => "Mini-Insurance (reference domain pack)";
    public override string ResultsDomainKey => "insurance";

    protected override string DefaultWorkflowName => "mini-insurance-first-delta";

    protected override void PrintDomainHelp(Action<string> log)
    {
        log("Mini-Insurance domain pack");
        log("Usage:");
        log("  domain mini-insurance pipeline [--no-learned]");
        log("  domain mini-insurance training-list");
        log("  domain mini-insurance training-inspect");
        log("  domain mini-insurance shift-training-inspect [workflowName]");
        log("  domain mini-insurance shift-training-history [workflowName] [maxItems]");
        log("  domain mini-insurance shift-training-best [workflowName]");
        log("");
        log("Defaults:");
        log("  workflowName = mini-insurance-first-delta");
        log("  domainKey    = insurance");
    }

    protected override async Task<int> ExecuteDomainCommandAsync(string sub, string[] args, Action<string> log)
    {
        switch (sub)
        {
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

            default:
                log($"Unknown subcommand '{sub}'.");
                PrintDomainHelp(log);
                return 1;
        }
    }
}
