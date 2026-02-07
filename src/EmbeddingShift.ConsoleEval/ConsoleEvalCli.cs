using System.Reflection;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Adaptive;
using EmbeddingShift.ConsoleEval.Commands;
using EmbeddingShift.ConsoleEval.Domains;
using EmbeddingShift.ConsoleEval.Repositories;
using EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Core.Infrastructure;
using EmbeddingShift.Workflows;

namespace EmbeddingShift.ConsoleEval;

internal static class ConsoleEvalCli
{
    private sealed record CommandSpec(string Name, string Summary, Func<string[], Task<int>> Handler);

    public static async Task<int> RunAsync(string[] args, ConsoleEvalHost host)
    {
        if (host is null) throw new ArgumentNullException(nameof(host));
        var services = host.Services;

        // Keep CLI runs isolated: some commands set Environment.ExitCode; do not leak across runs.
        Environment.ExitCode = 0;

        var commands = BuildCommands(host);

        // Global option: --tenant <key> (optional)
        // When provided, it is stored in ENV EMBEDDINGSHIFT_TENANT.
        // Mini-Insurance paths can then write under:
        //   results/insurance/tenants/<tenantKey>/...
        (var tenantKey, args) = ExtractGlobalTenantOption(args);
        if (!string.IsNullOrWhiteSpace(tenantKey))
            Environment.SetEnvironmentVariable("EMBEDDINGSHIFT_TENANT", tenantKey);


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


        // Note: per-command help is only supported at the top-level (and at the domain-pack root).
        // If a help token appears after the command name, treat it as unsupported nested help
        // and print the top-level help instead of mis-parsing it as a positional argument.
        if (!cmd.Equals("domain", StringComparison.OrdinalIgnoreCase) &&
            args.Length > 1 &&
            args.Skip(1).Any(IsHelp))
        {
            Console.WriteLine($"Note: per-command help is not implemented for '{cmd}' (help must be the first token).");
            Console.WriteLine("Use: dotnet run --project src/EmbeddingShift.ConsoleEval -- help");
            Console.WriteLine();
            PrintHelp(commands);
            return 0;
        }

        var exitCode = await spec.Handler(args);
        if (exitCode != 0)
            Environment.ExitCode = exitCode;

        return exitCode;
    }

    private static IReadOnlyDictionary<string, CommandSpec> BuildCommands(ConsoleEvalHost host)
    {
        var services = host.Services;

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
            a => DatasetCliCommands.IngestLegacyAsync(a, host));

        Add("ingest-dataset", "ingest refs+queries into FileStore (canonical)",
            a => DatasetCliCommands.IngestDatasetAsync(a, host),
            "ingest");

        Add("ingest-refs", "ingest refs only (plain)",
            a => DatasetCliCommands.IngestRefsAsync(a, host));

        Add("ingest-refs-chunked", "ingest refs only (chunked)",
            a => DatasetCliCommands.IngestRefsChunkedAsync(a, host));

        Add("ingest-queries", "ingest queries.json",
            a => DatasetCliCommands.IngestQueriesAsync(a, host));

        Add("ingest-inspect", "show ingest state/manifest for dataset (--role=refs|queries)",
       a => DatasetCliCommands.IngestInspectAsync(a));

        Add("dataset-status", "show dataset ingest status (state + manifests) (--role=refs|queries|all)",
           a => DatasetCliCommands.DatasetStatusAsync(a),
           "status");

        Add("dataset-reset", "reset dataset ingest artifacts (embeddings+manifests) (--role=refs|queries|all) [--force]",
            a => DatasetCliCommands.DatasetResetAsync(a),
            "reset");

        Add("dataset-validate", "validate dataset ingest artifacts (fast smoke gate) (--role=refs|queries|all)",
            a => DatasetCliCommands.DatasetValidateAsync(a),
            "validate");
        
        Add("eval", "evaluate from persisted embeddings (or --sim)",
           a => DatasetCliCommands.EvalAsync(a, host));

        Add("run", "ingest+eval in one go (arbitrary paths)",
            a => DatasetCliCommands.RunAsync(a, host));

        Add("run-demo", "run the demo insurance dataset",
            a => DatasetCliCommands.RunDemoAsync(a, host));

        Add("run-smoke", "ingest-dataset -> validate -> eval (fast smoke gate, persisted)",
            a => DatasetCliCommands.RunSmokeAsync(a, host),
            "smoke");

        Add("run-smoke-demo", "run-smoke using the built-in demo assets (no paths required)",
            a => DatasetCliCommands.RunSmokeDemoAsync(a, host),
            "smoke-demo");

        Add("smoke-all", "run: smoke-demo + mini-insurance pipeline + posneg (micro) (fast end-to-end)",
            a => SmokeCliCommands.SmokeAllAsync(a, host),
            "smoke");

        Add("runs-compare", "compare persisted run artifacts (run.json) under a root",
            WrapVoid(a => RunsCompareCommand.RunAsync(a.Skip(1).ToArray())));

        Add("runs-best", "select and optionally persist the best run (run.json) under a root",
            WrapVoid(a => RunsBestCommand.RunAsync(a.Skip(1).ToArray())));

        Add("runs-decide", "decide whether to promote the best run vs the active pointer (epsilon gate)",
            WrapVoid(a => RunsDecideCommand.RunAsync(a.Skip(1).ToArray())));

        Add("runs-promote", "promote best run to active pointer (with history backup)",
            WrapVoid(a => RunsPromoteCommand.RunAsync(a.Skip(1).ToArray())));

        Add("runs-rollback", "rollback active pointer to the latest archived active entry",
            WrapVoid(a => RunsRollbackCommand.RunAsync(a.Skip(1).ToArray())));

        Add("runs-history", "list activation history entries (archived active pointers)",
            WrapVoid(a => RunsHistoryCommand.RunAsync(a.Skip(1).ToArray())));

        Add("runs-active", "show current active run pointer",
            WrapVoid(a => RunsActiveCommand.RunAsync(a.Skip(1).ToArray())));

        Add("runs-rerun", "replay a run based on run_request.json (deterministic-friendly)",
            WrapVoid(a => RunsRerunCommand.RunAsync(a.Skip(1).ToArray())));
            WrapVoid(a => RunsActiveCommand.RunAsync(a.Skip(1).ToArray())));

        Add("runs-matrix", "run a batch of CLI variants described by a JSON spec (optional post-processing)",
            WrapVoid(a => RunsMatrixCommand.RunAsync(a.Skip(1).ToArray())));

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
    private static (string? TenantKey, string[] RemainingArgs) ExtractGlobalTenantOption(string[] args)
    {
        // Supported forms:
        //   --tenant <key>
        //   --tenant=<key>
        // This is intentionally dependency-free and small.
        string? tenant = null;
        var remaining = new List<string>(args.Length);

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i] ?? string.Empty;

            if (a.StartsWith("--tenant=", StringComparison.OrdinalIgnoreCase))
            {
                tenant = a.Substring("--tenant=".Length).Trim();
                continue;
            }

            if (a.Equals("--tenant", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    tenant = (args[i + 1] ?? string.Empty).Trim();
                    i++; // consume value
                }
                continue;
            }

            remaining.Add(a);
        }

        if (string.IsNullOrWhiteSpace(tenant))
            tenant = null;

        return (tenant, remaining.ToArray());
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
        Console.WriteLine("Global flags (may appear anywhere; removed before dispatch):");
        Console.WriteLine("  --tenant=<key>  |  --tenant <key>     (optional) writes Mini-Insurance under results/insurance/tenants/<key>/...");
        Console.WriteLine("  --provider=sim|openai-echo|openai-dryrun");
        Console.WriteLine("  --backend=sim|openai");
        Console.WriteLine("  --method=A");
        Console.WriteLine("  --sim-mode=deterministic|noisy");
        Console.WriteLine("  --sim-noise=<float>");
        Console.WriteLine("  --sim-algo=sha256|semantic-hash");
        Console.WriteLine("  --sim-char-ngrams=0|1");
        Console.WriteLine("  --semantic-cache | --no-semantic-cache");
        Console.WriteLine("  --cache-max=<int>  --cache-hamming=<int>  --cache-approx=0|1");
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
