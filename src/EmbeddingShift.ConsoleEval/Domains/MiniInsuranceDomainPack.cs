namespace EmbeddingShift.ConsoleEval.Domains;

using System;
using System.Linq;
using System.Threading.Tasks;
using EmbeddingShift.ConsoleEval.Commands;
using EmbeddingShift.ConsoleEval.MiniInsurance;
using EmbeddingShift.ConsoleEval;

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
        log("  domain mini-insurance posneg-train");
        log("  domain mini-insurance posneg-run");
        log("  domain mini-insurance posneg-inspect");
        log("  domain mini-insurance posneg-history [maxItems]");
        log("  domain mini-insurance posneg-best");
        log("  domain mini-insurance shift-training-inspect [workflowName]");
        log("  domain mini-insurance shift-training-history [workflowName] [maxItems]");
        log("  domain mini-insurance shift-training-best [workflowName]");
        log("");
        log("Defaults:");
        log("  workflowName = mini-insurance-first-delta");
        log("  domainKey    = insurance");
        log("  posneg workflowName = mini-insurance-posneg");
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

            case "posneg-train":
                {
                    log("[MiniInsurance] Training pos-neg learned global Delta shift (simulation backend)...");
                    log("");

                    var result = await MiniInsurancePosNegTrainer.TrainAsync(EmbeddingBackend.Sim);

                    log("[MiniInsurance] Pos-neg training finished.");
                    log($"  Workflow    : {result.WorkflowName}");
                    log($"  Runs        : {result.ComparisonRuns}");
                    log($"  Vector dim  : {result.DeltaVector?.Length ?? 0}");
                    log("");
                    log($"  Results root: {result.BaseDirectory}");
                    log("");
                    log("Next:");
                    log("  domain mini-insurance posneg-inspect");
                    log("  domain mini-insurance posneg-history [maxItems]");
                    log("  domain mini-insurance posneg-best");
                    log("");
                    log("Or (generic):");
                    log("  domain mini-insurance shift-training-inspect mini-insurance-posneg");
                    log("");

                    return 0;
                }

            case "posneg-run":
                {
                    log("[MiniInsurance] Running baseline vs pos-neg shift (simulation backend)...");
                    log("");
                    await MiniInsurancePosNegRunner.RunAsync(EmbeddingBackend.Sim);
                    return 0;
                }

            case "posneg-inspect":
                await ShiftTrainingInspectCommand.RunAsync(new[] { "mini-insurance-posneg", ResultsDomainKey });
                return 0;

            case "posneg-history":
                {
                    var maxItems = 20;
                    if (args.Length >= 2 && int.TryParse(args[1], out var parsed) && parsed > 0)
                        maxItems = parsed;

                    await ShiftTrainingHistoryCommand.RunAsync(
                        new[] { "mini-insurance-posneg", maxItems.ToString(), ResultsDomainKey });

                    return 0;
                }

            case "posneg-best":
                await ShiftTrainingBestCommand.RunAsync(new[] { "mini-insurance-posneg", ResultsDomainKey });
                return 0;

            default:
                log($"Unknown subcommand '{sub}'.");
                PrintDomainHelp(log);
                return 1;
        }
    }
}
