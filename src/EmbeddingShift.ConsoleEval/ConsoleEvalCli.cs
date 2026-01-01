using System;
using System.Linq;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Adaptive;
using EmbeddingShift.ConsoleEval.Commands;
using EmbeddingShift.ConsoleEval.Domains;
using EmbeddingShift.ConsoleEval.Repositories;
using EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Core.Infrastructure;
using EmbeddingShift.Workflows;
using EmbeddingShift.Workflows.Eval;
using EmbeddingShift.Workflows.Ingest;
using EmbeddingShift.Workflows.Run;

namespace EmbeddingShift.ConsoleEval;

internal static class ConsoleEvalCli
{
    public static async Task<int> RunAsync(
        string[] args,
        ConsoleEvalServices services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        var method = services.Method;
        var ingestEntry = services.IngestEntry;
        var ingestDatasetEntry = services.IngestDatasetEntry;
        var evalEntry = services.EvalEntry;
        var runEntry = services.RunEntry;
        var txtLineIngestor = services.TxtLineIngestor;
        var queriesJsonIngestor = services.QueriesJsonIngestor;


        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "domain":
                {
                    var exitCode = await DomainCliCommands.DomainAsync(args);
                    if (exitCode != 0)
                        Environment.ExitCode = exitCode;
                    break;
                }

            case "ingest-legacy":
                {
                    var exitCode = await DatasetCliCommands.IngestLegacyAsync(args, ingestEntry, txtLineIngestor);
                    if (exitCode != 0)
                        Environment.ExitCode = exitCode;
                    break;
                }

            case "eval":
                {
                    var exitCode = await DatasetCliCommands.EvalAsync(args, evalEntry);
                    if (exitCode != 0)
                    {
                        Environment.ExitCode = exitCode;
                        return exitCode;
                    }
                    break;
                }

            case "run":
                {
                    var exitCode = await DatasetCliCommands.RunAsync(args, runEntry, txtLineIngestor, queriesJsonIngestor);
                    if (exitCode != 0)
                    {
                        Environment.ExitCode = exitCode;
                        return exitCode;
                    }
                    break;
                }

            case "run-demo":
                {
                    var exitCode = await DatasetCliCommands.RunDemoAsync(args, runEntry, txtLineIngestor, queriesJsonIngestor);
                    if (exitCode != 0)
                    {
                        Environment.ExitCode = exitCode;
                        return exitCode;
                    }
                    break;
                }

            case "ingest-queries":
                {
                    var exitCode = await DatasetCliCommands.IngestQueriesAsync(args, ingestEntry, txtLineIngestor, queriesJsonIngestor);
                    if (exitCode != 0)
                        Environment.ExitCode = exitCode;
                    break;
                }

            case "ingest-dataset":
            case "ingest":
                {
                    var exitCode = await DatasetCliCommands.IngestDatasetAsync(args, ingestDatasetEntry, txtLineIngestor, queriesJsonIngestor);
                    if (exitCode != 0)
                        Environment.ExitCode = exitCode;
                    break;
                }

            case "ingest-refs":
                {
                    var exitCode = await DatasetCliCommands.IngestRefsAsync(args, ingestEntry, txtLineIngestor);
                    if (exitCode != 0)
                        Environment.ExitCode = exitCode;
                    break;
                }

            case "ingest-refs-chunked":
                {
                    var exitCode = await DatasetCliCommands.IngestRefsChunkedAsync(args, ingestEntry, txtLineIngestor);
                    if (exitCode != 0)
                        Environment.ExitCode = exitCode;
                    break;
                }

            case "ingest-inspect":
                {
                    var exitCode = await DatasetCliCommands.IngestInspectAsync(args);
                    if (exitCode != 0)
                        Environment.ExitCode = exitCode;
                    break;
                }

            case "adaptive":
                {
                    var workflowName = "mini-insurance-posneg";
                    var domainKey = "insurance";

                    if (args.Length > 1)
                    {
                        var position = 0;

                        for (var i = 1; i < args.Length; i++)
                        {
                            var token = args[i];
                            if (string.IsNullOrWhiteSpace(token))
                                continue;

                            if (token.StartsWith("-", StringComparison.Ordinal))
                                continue;

                            if (position == 0)
                                workflowName = token;
                            else if (position == 1)
                                domainKey = token;

                            position++;
                        }
                    }

                    var resultsRoot = DirectoryLayout.ResolveResultsRoot(domainKey);
                    var repository = new FileSystemShiftTrainingResultRepository(resultsRoot);

                    IShiftGenerator generator = new TrainingBackedShiftGenerator(
                        repository,
                        workflowName: workflowName);

                    var service = new ShiftEvaluationService(generator, EvaluatorCatalog.Defaults);
                    var wf = new AdaptiveWorkflow(generator, service, method);

                    Console.WriteLine($"Adaptive ready (method={method}, workflow={workflowName}, domain={domainKey}).");
                    AdaptiveDemo.RunDemo(wf);
                    break;
                }

            case "mini-insurance-adaptive":
            case "mini-insurance-adaptive-demo":
                {
                    goto case "adaptive";
                }

            case "help":
            case "--help":
            case "-h":
                {
                    Console.WriteLine("Commands:");
                    Console.WriteLine("  ingest-legacy   ingest refs only (plain) - legacy; prefer ingest-dataset / ingest");
                    Console.WriteLine("  ingest-dataset  ingest refs+queries into FileStore (canonical)");
                    Console.WriteLine("  ingest-refs     ingest refs only (plain)");
                    Console.WriteLine("  ingest-refs-chunked ingest refs only (chunked)");
                    Console.WriteLine("  ingest-inspect  show ingest state/manifest for dataset (--role=refs|queries)");
                    Console.WriteLine("  ingest-queries  ingest queries.json");
                    Console.WriteLine("  eval            evaluate from persisted embeddings (or --sim)");
                    Console.WriteLine("  run             ingest+eval in one go (arbitrary paths)");
                    Console.WriteLine("  run-demo        run the demo insurance dataset");
                    Console.WriteLine("  domain          domain-pack entrypoint (domain list / domain <id> ...)");
                    Console.WriteLine();
                    Console.WriteLine("Common flags:");
                    Console.WriteLine("  --provider=sim|openai   Select embedding provider backend");
                    Console.WriteLine();
                    return 0;
                }

            case "mini-insurance":
                {
                    await MiniInsuranceLegacyCliCommands.RunMiniInsuranceAsync();
                    break;
                }

            case "mini-insurance-first-delta":
                {
                    await MiniInsuranceLegacyCliCommands.RunMiniInsuranceFirstDeltaAsync();
                    break;
                }

            // Compatibility with my earlier (wrong) suggestions:
            case "mini-insurance-first-shift":
            case "mini-insurance-first-shift-and-delta":
                {
                    Console.WriteLine("Deprecated alias. Use: mini-insurance-first-delta");
                    goto case "mini-insurance-first-delta";
                }

            case "mini-insurance-first-delta-pipeline":
                {
                    var pack = DomainPackRegistry.TryGet("mini-insurance");
                    if (pack is null)
                    {
                        Console.WriteLine("Unknown domain pack 'mini-insurance'.");
                        Environment.ExitCode = 1;
                        break;
                    }

                    var subArgs = new[] { "pipeline" }.Concat(args.Skip(1)).ToArray();
                    var exitCode = await pack.ExecuteAsync(subArgs, msg => Console.WriteLine(msg));
                    if (exitCode != 0)
                        Environment.ExitCode = exitCode;

                    break;
                }

            case "mini-insurance-training-inspect":
                await DomainCliCommands.ExecuteDomainPackAsync(
                    "mini-insurance",
                    new[] { "training-inspect" }.Concat(args.Skip(1)).ToArray());
                break;

            case "mini-insurance-training-list":
                await DomainCliCommands.ExecuteDomainPackAsync(
                    "mini-insurance",
                    new[] { "training-list" }.Concat(args.Skip(1)).ToArray());
                break;

            case "mini-insurance-first-delta-aggregate":
                {
                    MiniInsuranceLegacyCliCommands.AggregateFirstDelta();
                    break;
                }

            case "shift-training-inspect":
                {
                    await ShiftTrainingInspectCommand.RunAsync(args.Skip(1).ToArray());
                    break;
                }

            case "shift-training-history":
                {
                    await ShiftTrainingHistoryCommand.RunAsync(args.Skip(1).ToArray());
                    break;
                }

            case "shift-training-best":
                {
                    await ShiftTrainingBestCommand.RunAsync(args.Skip(1).ToArray());
                    break;
                }

            case "mini-insurance-first-delta-train":
                {
                    MiniInsuranceLegacyCliCommands.TrainFirstDelta();
                    break;
                }

            case "mini-insurance-first-learned-delta":
                {
                    await MiniInsuranceLegacyCliCommands.RunMiniInsuranceFirstLearnedDeltaAsync();
                    break;
                }

            case "mini-insurance-first-delta-inspect":
                {
                    MiniInsuranceLegacyCliCommands.InspectFirstDeltaCandidate();
                    break;
                }

            case "mini-insurance-shift-training-inspect":
                await DomainCliCommands.ExecuteDomainPackAsync(
                    "mini-insurance",
                    new[] { "shift-training-inspect" }.Concat(args.Skip(1)).ToArray());
                break;

            case "mini-insurance-shift-training-history":
                await DomainCliCommands.ExecuteDomainPackAsync(
                    "mini-insurance",
                    new[] { "shift-training-history" }.Concat(args.Skip(1)).ToArray());
                break;

            case "mini-insurance-shift-training-best":
                await DomainCliCommands.ExecuteDomainPackAsync(
                    "mini-insurance",
                    new[] { "shift-training-best" }.Concat(args.Skip(1)).ToArray());
                break;

            case "mini-insurance-posneg-train":
                await DomainCliCommands.ExecuteDomainPackAsync(
                    "mini-insurance",
                    new[] { "posneg-train" }.Concat(args.Skip(1)).ToArray());
                break;

            case "mini-insurance-posneg-training-inspect":
                await DomainCliCommands.ExecuteDomainPackAsync(
                    "mini-insurance",
                    new[] { "posneg-inspect" }.Concat(args.Skip(1)).ToArray());
                break;

            case "mini-insurance-posneg-training-history":
                await DomainCliCommands.ExecuteDomainPackAsync(
                    "mini-insurance",
                    new[] { "posneg-history" }.Concat(args.Skip(1)).ToArray());
                break;

            case "mini-insurance-posneg-training-best":
                await DomainCliCommands.ExecuteDomainPackAsync(
                    "mini-insurance",
                    new[] { "posneg-best" }.Concat(args.Skip(1)).ToArray());
                break;

            case "mini-insurance-posneg-run":
                await DomainCliCommands.ExecuteDomainPackAsync(
                    "mini-insurance",
                    new[] { "posneg-run" }.Concat(args.Skip(1)).ToArray());
                break;

            case "--version":
                {
                    var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "dev";
                    Console.WriteLine($".NET ConsoleEval version {v}");
                    break;
                }

            default:
                {
                    PrintHelp();
                    break;
                }
        }

        return Environment.ExitCode;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("EmbeddingShift.ConsoleEval — usage");
        Console.WriteLine("  help | --help | -h    show command list");
        Console.WriteLine("  domain list           list domain packs");
        Console.WriteLine("  run-demo              run demo dataset");
        Console.WriteLine("  eval <dataset>        evaluate dataset");
        Console.WriteLine("  adaptive              run adaptive demo");
        Console.WriteLine();
        Console.WriteLine("Tip: run with 'help' for the full list.");
    }
}
