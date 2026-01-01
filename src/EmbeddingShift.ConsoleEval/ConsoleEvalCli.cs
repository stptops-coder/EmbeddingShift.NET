// [FILE] src/EmbeddingShift.ConsoleEval/ConsoleEvalCli.cs
// [ACTION] REPLACE WHOLE FILE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    private sealed record CommandSpec(string Name, string Summary, Func<string[], Task<int>> Handler);

    public static async Task<int> RunAsync(string[] args, ConsoleEvalServices services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        // Keep CLI runs isolated: some commands set Environment.ExitCode; do not leak across runs.
        Environment.ExitCode = 0;

        var commands = BuildCommands(services);

        if (args.Length == 0)
        {
            PrintHelp(commands);
            return 0;
        }

        var cmd = args[0];

        if (IsHelp(cmd))
        {
            PrintHelp(commands);
            return 0;
        }

        if (cmd.Equals("--version", StringComparison.OrdinalIgnoreCase))
        {
            PrintVersion();
            return 0;
        }

        if (!commands.TryGetValue(cmd, out var spec))
        {
            Console.WriteLine($"Unknown command '{cmd}'.");
            Console.WriteLine();
            PrintHelp(commands);
            Environment.ExitCode = 1;
            return 1;
        }

        var exitCode = await spec.Handler(args);
        if (exitCode != 0)
            Environment.ExitCode = exitCode;

        return exitCode;
    }

    private static IReadOnlyDictionary<string, CommandSpec> BuildCommands(ConsoleEvalServices services)
    {
        var method = services.Method;
        var ingestEntry = services.IngestEntry;
        var ingestDatasetEntry = services.IngestDatasetEntry;
        var evalEntry = services.EvalEntry;
        var runEntry = services.RunEntry;
        var txtLineIngestor = services.TxtLineIngestor;
        var queriesJsonIngestor = services.QueriesJsonIngestor;

        var map = new Dictionary<string, CommandSpec>(StringComparer.OrdinalIgnoreCase);

        void Add(string name, string summary, Func<string[], Task<int>> handler, params string[] aliases)
        {
            var spec = new CommandSpec(name, summary, handler);
            map[name] = spec;

            if (aliases is null) return;
            foreach (var a in aliases.Where(x => !string.IsNullOrWhiteSpace(x)))
                map[a] = spec;
        }

        static Func<string[], Task<int>> WrapVoid(Func<string[], Task> fn)
            => async a =>
            {
                await fn(a);
                return Environment.ExitCode;
            };

        Add("domain", "domain-pack entrypoint (domain list / domain <id> ...)",
            a => DomainCliCommands.DomainAsync(a));

        Add("ingest-legacy", "ingest refs only (plain) - legacy; prefer ingest-dataset / ingest",
            a => DatasetCliCommands.IngestLegacyAsync(a, ingestEntry, txtLineIngestor));

        Add("ingest-dataset", "ingest refs+queries into FileStore (canonical)",
            a => DatasetCliCommands.IngestDatasetAsync(a, ingestDatasetEntry, txtLineIngestor, queriesJsonIngestor),
            "ingest");

        Add("ingest-refs", "ingest refs only (plain)",
            a => DatasetCliCommands.IngestRefsAsync(a, ingestEntry, txtLineIngestor));

        Add("ingest-refs-chunked", "ingest refs only (chunked)",
            a => DatasetCliCommands.IngestRefsChunkedAsync(a, ingestEntry, txtLineIngestor));

        Add("ingest-queries", "ingest queries.json",
            a => DatasetCliCommands.IngestQueriesAsync(a, ingestEntry, txtLineIngestor, queriesJsonIngestor));

        Add("ingest-inspect", "show ingest state/manifest for dataset (--role=refs|queries)",
            a => DatasetCliCommands.IngestInspectAsync(a));

        Add("eval", "evaluate from persisted embeddings (or --sim)",
            a => DatasetCliCommands.EvalAsync(a, evalEntry));

        Add("run", "ingest+eval in one go (arbitrary paths)",
            a => DatasetCliCommands.RunAsync(a, runEntry, txtLineIngestor, queriesJsonIngestor));

        Add("run-demo", "run the demo insurance dataset",
            a => DatasetCliCommands.RunDemoAsync(a, runEntry, txtLineIngestor, queriesJsonIngestor));

        Add("adaptive", "run adaptive demo (optional args: <workflowName> <domainKey>)",
            a => RunAdaptiveAsync(a, method),
            "mini-insurance-adaptive",
            "mini-insurance-adaptive-demo");

        // Legacy mini-insurance commands (kept for compatibility / existing scripts)
        Add("mini-insurance", "legacy mini-insurance demo",
            WrapVoid(async _ => await MiniInsuranceLegacyCliCommands.RunMiniInsuranceAsync()));

        Add("mini-insurance-first-delta", "legacy first-delta mini-insurance demo",
            WrapVoid(async _ => await MiniInsuranceLegacyCliCommands.RunMiniInsuranceFirstDeltaAsync()));

        Add("mini-insurance-first-shift", "deprecated alias -> mini-insurance-first-delta",
            WrapVoid(async _ =>
            {
                Console.WriteLine("Deprecated alias. Use: mini-insurance-first-delta");
                await MiniInsuranceLegacyCliCommands.RunMiniInsuranceFirstDeltaAsync();
            }),
            "mini-insurance-first-shift-and-delta");

        Add("mini-insurance-first-learned-delta", "legacy first learned-delta mini-insurance demo",
            WrapVoid(async _ => await MiniInsuranceLegacyCliCommands.RunMiniInsuranceFirstLearnedDeltaAsync()));

        Add("mini-insurance-first-delta-aggregate", "aggregate first-delta candidates (legacy)",
            _ =>
            {
                MiniInsuranceLegacyCliCommands.AggregateFirstDelta();
                return Task.FromResult(0);
            });

        Add("mini-insurance-first-delta-train", "train first-delta (legacy)",
            _ =>
            {
                MiniInsuranceLegacyCliCommands.TrainFirstDelta();
                return Task.FromResult(0);
            });

        Add("mini-insurance-first-delta-inspect", "inspect first-delta candidate (legacy)",
            _ =>
            {
                MiniInsuranceLegacyCliCommands.InspectFirstDeltaCandidate();
                return Task.FromResult(0);
            });

        Add("mini-insurance-first-delta-pipeline", "domain-pack pipeline entry (mini-insurance)",
            a => RunMiniInsurancePipelineAsync(a));

        Add("mini-insurance-training-inspect", "domain-pack: training-inspect (mini-insurance)",
            a => DomainCliCommands.ExecuteDomainPackAsync(
                "mini-insurance",
                new[] { "training-inspect" }.Concat(a.Skip(1)).ToArray()));

        Add("mini-insurance-training-list", "domain-pack: training-list (mini-insurance)",
            a => DomainCliCommands.ExecuteDomainPackAsync(
                "mini-insurance",
                new[] { "training-list" }.Concat(a.Skip(1)).ToArray()));

        Add("mini-insurance-shift-training-inspect", "domain-pack: shift-training-inspect (mini-insurance)",
            a => DomainCliCommands.ExecuteDomainPackAsync(
                "mini-insurance",
                new[] { "shift-training-inspect" }.Concat(a.Skip(1)).ToArray()));

        Add("mini-insurance-shift-training-history", "domain-pack: shift-training-history (mini-insurance)",
            a => DomainCliCommands.ExecuteDomainPackAsync(
                "mini-insurance",
                new[] { "shift-training-history" }.Concat(a.Skip(1)).ToArray()));

        Add("mini-insurance-shift-training-best", "domain-pack: shift-training-best (mini-insurance)",
            a => DomainCliCommands.ExecuteDomainPackAsync(
                "mini-insurance",
                new[] { "shift-training-best" }.Concat(a.Skip(1)).ToArray()));

        Add("mini-insurance-posneg-train", "domain-pack: posneg-train (mini-insurance)",
            a => DomainCliCommands.ExecuteDomainPackAsync(
                "mini-insurance",
                new[] { "posneg-train" }.Concat(a.Skip(1)).ToArray()));

        Add("mini-insurance-posneg-training-inspect", "domain-pack: posneg-inspect (mini-insurance)",
            a => DomainCliCommands.ExecuteDomainPackAsync(
                "mini-insurance",
                new[] { "posneg-inspect" }.Concat(a.Skip(1)).ToArray()));

        Add("mini-insurance-posneg-training-history", "domain-pack: posneg-history (mini-insurance)",
            a => DomainCliCommands.ExecuteDomainPackAsync(
                "mini-insurance",
                new[] { "posneg-history" }.Concat(a.Skip(1)).ToArray()));

        Add("mini-insurance-posneg-training-best", "domain-pack: posneg-best (mini-insurance)",
            a => DomainCliCommands.ExecuteDomainPackAsync(
                "mini-insurance",
                new[] { "posneg-best" }.Concat(a.Skip(1)).ToArray()));

        Add("mini-insurance-posneg-run", "domain-pack: posneg-run (mini-insurance)",
            a => DomainCliCommands.ExecuteDomainPackAsync(
                "mini-insurance",
                new[] { "posneg-run" }.Concat(a.Skip(1)).ToArray()));

        // Shift-training commands (generic, not domain-scoped)
        Add("shift-training-inspect", "inspect a shift training runroot",
            WrapVoid(a => ShiftTrainingInspectCommand.RunAsync(a.Skip(1).ToArray())));

        Add("shift-training-history", "show shift training history",
            WrapVoid(a => ShiftTrainingHistoryCommand.RunAsync(a.Skip(1).ToArray())));

        Add("shift-training-best", "show best shift training candidate",
            WrapVoid(a => ShiftTrainingBestCommand.RunAsync(a.Skip(1).ToArray())));

        return map;
    }

    private static bool IsHelp(string cmd)
    {
        return cmd.Equals("help", StringComparison.OrdinalIgnoreCase) ||
               cmd.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
               cmd.Equals("-h", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "dev";
        Console.WriteLine($".NET ConsoleEval version {v}");
    }

    private static void PrintHelp(IReadOnlyDictionary<string, CommandSpec> commandMap)
    {
        Console.WriteLine("EmbeddingShift.ConsoleEval — usage");
        Console.WriteLine("  dotnet run --project src/EmbeddingShift.ConsoleEval -- <command> [args]");
        Console.WriteLine();
        Console.WriteLine("Commands:");

        var unique = commandMap
            .Values
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var spec in unique)
            Console.WriteLine($"  {spec.Name,-28} {spec.Summary}");

        Console.WriteLine();
        Console.WriteLine("Tip: dotnet run --project src/EmbeddingShift.ConsoleEval -- help");
        Console.WriteLine("Also: --version");
    }

    private static Task<int> RunAdaptiveAsync(string[] args, ShiftMethod method)
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

        return Task.FromResult(0);
    }

    private static async Task<int> RunMiniInsurancePipelineAsync(string[] args)
    {
        var pack = DomainPackRegistry.TryGet("mini-insurance");
        if (pack is null)
        {
            Console.WriteLine("Unknown domain pack 'mini-insurance'.");
            return 1;
        }

        var subArgs = new[] { "pipeline" }.Concat(args.Skip(1)).ToArray();
        var exitCode = await pack.ExecuteAsync(subArgs, msg => Console.WriteLine(msg));
        if (exitCode != 0)
            Environment.ExitCode = exitCode;

        return exitCode;
    }
}
