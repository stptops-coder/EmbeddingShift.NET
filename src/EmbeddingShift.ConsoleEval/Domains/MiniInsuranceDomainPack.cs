namespace EmbeddingShift.ConsoleEval.Domains;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using EmbeddingShift.ConsoleEval.Commands;
using EmbeddingShift.ConsoleEval.MiniInsurance;
using EmbeddingShift.ConsoleEval;
using EmbeddingShift.Core.Training;
using EmbeddingShift.Core.Runs;


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
        log("  domain mini-insurance pipeline [--no-learned] [--query-policy=<path>]");
        log("  domain mini-insurance training-list");
        log("  domain mini-insurance training-inspect");
        log("  domain mini-insurance posneg-train [--mode=micro|production] [--cancel-epsilon=<float>]");
        log("  domain mini-insurance posneg-run");
        log("  domain mini-insurance posneg-inspect");
        log("  domain mini-insurance posneg-history [maxItems]");
        log("  domain mini-insurance posneg-best");
        log("");
        log("  domain mini-insurance dataset-generate <name> [--tenant=<id>] [--stages=N] [--policies=N] [--queries=N] [--seed=N] [--overwrite]");
        log("    Writes staged datasets under: results/insurance/tenants/<tenant>/datasets/<name>/stage-00 ...");
        log("    Use env var to switch the workflow input:");
        log("      EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT=<full-or-repo-relative-stage-00-path>");
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

                    static string? ReadOpt(string[] allArgs, string optName)
                    {
                        for (int i = 0; i < allArgs.Length; i++)
                        {
                            var a = allArgs[i] ?? string.Empty;

                            if (a.StartsWith(optName + "=", StringComparison.OrdinalIgnoreCase))
                            {
                                return a.Substring(optName.Length + 1).Trim().Trim('"');
                            }

                            if (string.Equals(a, optName, StringComparison.OrdinalIgnoreCase) && i + 1 < allArgs.Length)
                            {
                                return (allArgs[i + 1] ?? string.Empty).Trim().Trim('"');
                            }
                        }

                        return null;
                    }

                    var queryPolicyPath = ReadOpt(args, "--query-policy");

                    var commandArgs = new List<string> { "domain", "mini-insurance", "pipeline" };

                    if (!includeLearned)
                        commandArgs.Add("--no-learned");

                    if (!string.IsNullOrWhiteSpace(queryPolicyPath))
                        commandArgs.Add($"--query-policy={queryPolicyPath}");

                    var request = RunRequestFactory.Create(
                        commandArgs: commandArgs.ToArray(),
                        notes: "Captured from environment for replay (mini-insurance pipeline).");

                    using var _ = RunRequestContext.Push(request);

                    var pipeline = new MiniInsuranceFirstDeltaPipeline(log);
                    await pipeline.RunAsync(includeLearnedDelta: includeLearned, queryPolicyPath: queryPolicyPath);
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

                    var mode = TrainingMode.Production;
                    var cancelEpsilon = 1e-3f;
                    var hardNegTopK = 1;

                    foreach (var a in args.Skip(1))
                    {
                        if (a.StartsWith("--mode=", StringComparison.OrdinalIgnoreCase))
                        {
                            var value = a.Split('=', 2)[1].Trim();
                            if (value.Equals("micro", StringComparison.OrdinalIgnoreCase))
                                mode = TrainingMode.Micro;
                            else if (value.Equals("prod", StringComparison.OrdinalIgnoreCase) || value.Equals("production", StringComparison.OrdinalIgnoreCase))
                                mode = TrainingMode.Production;
                        }
                        else if (a.StartsWith("--hardneg-topk=", StringComparison.OrdinalIgnoreCase))
                        {
                            var value = a.Split('=', 2)[1].Trim();
                            if (int.TryParse(value, out var parsed) && parsed > 0)
                                hardNegTopK = parsed;
                        }
                    }

                    log($"  Mode        : {mode}");
                    log($"  Cancel eps  : {cancelEpsilon:0.000000E+0}");
                    log("");

                    var result = await MiniInsurancePosNegTrainer.TrainAsync(
                        EmbeddingBackend.Sim,
                        mode,
                        cancelEpsilon,
                        hardNegTopK);

                    log("[MiniInsurance] Pos-neg training finished.");
                    log($"  Workflow    : {result.WorkflowName}");
                    log($"  Runs        : {result.ComparisonRuns}");
                    log($"  Vector dim  : {result.DeltaVector?.Length ?? 0}");
                    log("");
                    log($"  Results root: {result.BaseDirectory}");
                    log("");
                    log("Next:");
                    log("  domain mini-insurance posneg-inspect");
                    log("  domain mini-insurance posneg-history [maxItems] [--include-cancelled]");
                    log("  domain mini-insurance posneg-best [--include-cancelled]");
                    log("  domain mini-insurance runroot-summarize [--runroot=<path>] [--out=<path>]");
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

                    var useLatest =
                        args.Any(a => string.Equals(a, "--latest", StringComparison.OrdinalIgnoreCase));

                    double scale = 1.0;
                    foreach (var a in args)
                    {
                        if (a.StartsWith("--scale=", StringComparison.OrdinalIgnoreCase))
                        {
                            var v = a.Split('=', 2)[1].Trim();
                            if (double.TryParse(v, System.Globalization.NumberStyles.Float,
                                    System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                            {
                                scale = parsed;
                            }
                        }
                    }

                    await MiniInsurancePosNegRunner.RunAsync(EmbeddingBackend.Sim, useLatest: useLatest, scale: scale);
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

                    var includeCancelled =
                        args.Any(a => string.Equals(a, "--include-cancelled", StringComparison.OrdinalIgnoreCase));

                    await ShiftTrainingHistoryCommand.RunAsync(
                        includeCancelled
                            ? new[] { "mini-insurance-posneg", maxItems.ToString(), ResultsDomainKey, "--include-cancelled" }
                            : new[] { "mini-insurance-posneg", maxItems.ToString(), ResultsDomainKey });


                    return 0;
                }

            case "posneg-best":
                { 
                var includeCancelled =
                    args.Any(a => string.Equals(a, "--include-cancelled", StringComparison.OrdinalIgnoreCase));

                await ShiftTrainingBestCommand.RunAsync(
                    includeCancelled
                        ? new[] { "mini-insurance-posneg", ResultsDomainKey, "--include-cancelled" }
                        : new[] { "mini-insurance-posneg", ResultsDomainKey });
                return 0;
                }

            case "segment-compare":
                {
                    // usage:
                    // domain mini-insurance segment-compare --segments <path> [--metric ndcg@3]
                    string? segments = null;
                    string metric = "ndcg@3";

                    for (int i = 0; i < args.Length; i++)
                    {
                        var a = args[i];
                        if (a.StartsWith("--segments=", StringComparison.OrdinalIgnoreCase))
                            segments = a.Split('=', 2)[1];
                        else if (string.Equals(a, "--segments", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                            segments = args[++i];
                        else if (a.StartsWith("--metric=", StringComparison.OrdinalIgnoreCase))
                            metric = a.Split('=', 2)[1];
                        else if (string.Equals(a, "--metric", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                            metric = args[++i];
                    }

                    if (string.IsNullOrWhiteSpace(segments))
                        throw new ArgumentException("Missing --segments <path>");

                    return MiniInsuranceSegmentCompare.Run(segments, metric);
                }

            case "dataset-generate":
                {
                    if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
                    {
                        log("Missing dataset name.");
                        PrintDomainHelp(log);
                        return 1;
                    }

                    var name = args[1].Trim();

                    int ReadIntOpt(string key, int @default)
                    {
                        // supports: --key=123  OR  --key 123
                        for (var i = 0; i < args.Length; i++)
                        {
                            var a = args[i];
                            if (a.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase) &&
                                int.TryParse(a[(key.Length + 1)..], out var v)) return v;

                            if (string.Equals(a, key, StringComparison.OrdinalIgnoreCase) &&
                                i + 1 < args.Length &&
                                int.TryParse(args[i + 1], out var v2)) return v2;
                        }
                        return @default;
                    }

                    var stages = ReadIntOpt("--stages", 3);
                    var policies = ReadIntOpt("--policies", 40);
                    var queries = ReadIntOpt("--queries", 80);
                    var seed = ReadIntOpt("--seed", 1337);
                    var overwrite = args.Any(a => string.Equals(a, "--overwrite", StringComparison.OrdinalIgnoreCase));

                    MiniInsuranceStagedDatasetGenerator.Generate(
                        datasetName: name,
                        stages: stages,
                        basePolicies: policies,
                        baseQueries: queries,
                        seed: seed,
                        overwrite: overwrite,
                        log: log);

                    return 0;
                }

            case "runroot-summarize":
                {
                    // Writes a compact summary report under:
                    //   <runroot>\\results\\insurance\\reports\\summary.txt
                    //
                    // Uses --runroot=<path> or ENV:EMBEDDINGSHIFT_ROOT
                    await MiniInsuranceRunRootSummarizeCommand.RunAsync(args);
                    return 0;
                }

            default:
                log($"Unknown subcommand '{sub}'.");
                PrintDomainHelp(log);
                return 1;
        }
    }
}
