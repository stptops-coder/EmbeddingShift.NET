using EmbeddingShift.Abstractions;           // ShiftMethod
using EmbeddingShift.Adaptive;               // ShiftEvaluationService
using EmbeddingShift.ConsoleEval;
using EmbeddingShift.ConsoleEval.Domains;
using EmbeddingShift.ConsoleEval.Commands;
using EmbeddingShift.ConsoleEval.Inspector;
using EmbeddingShift.Core.Evaluators;        // EvaluatorCatalog
using EmbeddingShift.Core.Generators;        // DeltaShiftGenerator (example)
using EmbeddingShift.Core.Infrastructure;    // DirectoryLayout for /data and /results roots
using EmbeddingShift.Core.Runs;        // RunPersistor
using EmbeddingShift.Core.Stats;       // InMemoryStatsCollector
using EmbeddingShift.Core.Workflows;   // StatsAwareWorkflowRunner + ReportMarkdown
using EmbeddingShift.Workflows;              // AdaptiveWorkflow
using EmbeddingShift.Workflows.Ingest;       // DatasetIngestEntry (canonical ingest entrypoint)
using EmbeddingShift.Workflows.Run;
using EmbeddingShift.Workflows.Eval;
using System.Globalization;
using EmbeddingShift.Preprocessing;
using EmbeddingShift.Preprocessing.Chunking;
using EmbeddingShift.Preprocessing.Loading;
using EmbeddingShift.Preprocessing.Transform;
using System.Linq;


// Composition Root (kept simple)
// Flags:
//   --provider=sim | openai-echo | openai-dryrun    (default: sim)
string providerArg = args.FirstOrDefault(a => a.StartsWith("--provider=", StringComparison.OrdinalIgnoreCase))
                    ?.Split('=', 2)[1] ?? "sim";

// CLI-level override of embedding/simulation environment options (if present)
// Note: args[0] must remain the command (e.g., "domain"), so place these flags AFTER the command.
//
// Flags:
//   --sim-mode=deterministic|noisy
//   --sim-noise=<float>
//   --sim-algo=sha256|semantic-hash
//   --sim-char-ngrams=0|1
//   --semantic-cache | --no-semantic-cache
//   --cache-max=<int>
//   --cache-hamming=<int>
//   --cache-approx=0|1
string? simModeArg = null;
string? simNoiseArg = null;
string? simAlgoArg = null;
string? simCharNGramsArg = null;

bool? semanticCacheArg = null;
string? cacheMaxArg = null;
string? cacheHammingArg = null;
string? cacheApproxArg = null;

foreach (var arg in args)
{
    if (arg.StartsWith("--sim-mode=", StringComparison.OrdinalIgnoreCase))
    {
        simModeArg = arg.Substring("--sim-mode=".Length);
    }
    else if (arg.StartsWith("--sim-noise=", StringComparison.OrdinalIgnoreCase))
    {
        simNoiseArg = arg.Substring("--sim-noise=".Length);
    }
    else if (arg.StartsWith("--sim-algo=", StringComparison.OrdinalIgnoreCase))
    {
        simAlgoArg = arg.Substring("--sim-algo=".Length);
    }
    else if (arg.StartsWith("--sim-char-ngrams=", StringComparison.OrdinalIgnoreCase))
    {
        simCharNGramsArg = arg.Substring("--sim-char-ngrams=".Length);
    }
    else if (arg.Equals("--semantic-cache", StringComparison.OrdinalIgnoreCase))
    {
        semanticCacheArg = true;
    }
    else if (arg.Equals("--no-semantic-cache", StringComparison.OrdinalIgnoreCase))
    {
        semanticCacheArg = false;
    }
    else if (arg.StartsWith("--cache-max=", StringComparison.OrdinalIgnoreCase))
    {
        cacheMaxArg = arg.Substring("--cache-max=".Length);
    }
    else if (arg.StartsWith("--cache-hamming=", StringComparison.OrdinalIgnoreCase))
    {
        cacheHammingArg = arg.Substring("--cache-hamming=".Length);
    }
    else if (arg.StartsWith("--cache-approx=", StringComparison.OrdinalIgnoreCase))
    {
        cacheApproxArg = arg.Substring("--cache-approx=".Length);
    }
}

if (!string.IsNullOrWhiteSpace(simModeArg))
{
    Environment.SetEnvironmentVariable("EMBEDDING_SIM_MODE", simModeArg);
}

if (!string.IsNullOrWhiteSpace(simNoiseArg))
{
    Environment.SetEnvironmentVariable("EMBEDDING_SIM_NOISE_AMPLITUDE", simNoiseArg);
}

if (!string.IsNullOrWhiteSpace(simAlgoArg))
{
    Environment.SetEnvironmentVariable("EMBEDDING_SIM_ALGO", simAlgoArg);
}

if (!string.IsNullOrWhiteSpace(simCharNGramsArg))
{
    Environment.SetEnvironmentVariable("EMBEDDING_SIM_SEMANTIC_CHAR_NGRAMS", simCharNGramsArg);
}

if (semanticCacheArg.HasValue)
{
    Environment.SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE", semanticCacheArg.Value ? "1" : "0");
}

if (!string.IsNullOrWhiteSpace(cacheMaxArg))
{
    Environment.SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE_MAX", cacheMaxArg);
}

if (!string.IsNullOrWhiteSpace(cacheHammingArg))
{
    Environment.SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE_HAMMING", cacheHammingArg);
}

if (!string.IsNullOrWhiteSpace(cacheApproxArg))
{
    Environment.SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE_APPROX", cacheApproxArg);
}

// base provider used by all modes (still the existing SimEmbeddingProvider)
IEmbeddingProvider baseProvider = EmbeddingProviderFactory.FromEnvironment();
EmbeddingConsoleDiagnostics.PrintEmbeddingConfiguration();

IEmbeddingProvider provider = providerArg.ToLowerInvariant() switch
{
    "openai-echo" => new EmbeddingShift.Providers.OpenAI.EchoEmbeddingProvider(baseProvider),
    "openai-dryrun" => new EmbeddingShift.Providers.OpenAI.DryRunEmbeddingProvider(baseProvider),
    _ => baseProvider
};

Console.WriteLine($"[BOOT] Embedding provider = {provider.Name}");

IRunLogger logger = new ConsoleRunLogger();
var runner = EvaluationRunner.WithDefaults(logger);

// Mode switch: default = Shifted; use --method=A to force identity (NoShiftIngestBased).
var method = args.Any(a => a.Equals("--method=A", StringComparison.OrdinalIgnoreCase))
    ? ShiftMethod.NoShiftIngestBased
    : ShiftMethod.Shifted;

// Demo ingest components
IIngestor ingestor = new MinimalTxtIngestor();

// File-based vector store for persistence (kept out of bin/Debug)
var storeRoot = DirectoryLayout.ResolveDataRoot();
IVectorStore store = new EmbeddingShift.Core.Persistence.FileStore(storeRoot);

// Canonical ingest entrypoint (domain-neutral; reusable for CLI and future UI).
var ingestEntry = new DatasetIngestEntry(provider, store);
var txtLineIngestor = new MinimalTxtIngestor();
var queriesJsonIngestor = new JsonQueryIngestor();

var evalWf = new EvaluationWorkflow(runner);
var evalEntry = new DatasetEvalEntry(provider, evalWf);
var runEntry = new DatasetRunEntry(ingestEntry, evalEntry);


static string ResolveSamplesDemoPath()
{
    // dataRoot = <repo-root>/data
    var dataRoot = DirectoryLayout.ResolveDataRoot();
    var repoRoot = Path.GetFullPath(Path.Combine(dataRoot, ".."));
    if (RepositoryLayout.TryResolveRepoRoot(out var rr))
        repoRoot = rr;

    return Path.Combine(repoRoot, "samples", "demo");
}
static async Task ExecuteDomainPackAsync(string domainId, string[] subArgs)
{
    var pack = DomainPackRegistry.TryGet(domainId);
    if (pack is null)
    {
        Console.WriteLine($"Unknown domain pack '{domainId}'.");
        Environment.ExitCode = 1;
        return;
    }

    var exitCode = await pack.ExecuteAsync(subArgs, msg => Console.WriteLine(msg));
    if (exitCode != 0)
        Environment.ExitCode = exitCode;
}


// --- CLI ---
if (args.Length == 0)
{
    Helpers.PrintHelp();
    Console.WriteLine("  adaptive [--baseline]     - adaptive shift selection (Baseline = identity)");
    return;
}

switch (args[0].ToLowerInvariant())
{
    case "domain":
        {
            // Entry point for domain packs (towards multi-domain ConsoleEval).
            //
            // Usage:
            //   domain list
            //   domain <domainId> <subcommand> [...]

            var sub = args.Length >= 2 ? args[1] : "list";

            if (string.Equals(sub, "list", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sub, "--list", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Available domain packs:");
                foreach (var pack in DomainPackRegistry.All)
                {
                    Console.WriteLine($"  {pack.DomainId,-18} {pack.DisplayName}");
                }
                Console.WriteLine();
                Console.WriteLine("Example:");
                Console.WriteLine("  dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance pipeline");
                break;
            }

            var packById = DomainPackRegistry.TryGet(sub);
            if (packById is null)
            {
                Console.WriteLine($"Unknown domain pack '{sub}'.");
                Console.WriteLine();
                Console.WriteLine("Use:");
                Console.WriteLine("  domain list");
                break;
            }

            var subArgs = args.Skip(2).ToArray();
            var exitCode = await packById.ExecuteAsync(subArgs, msg => Console.WriteLine(msg));
            if (exitCode != 0)
            {
                Environment.ExitCode = exitCode;
            }

            break;
        }
    case "ingest":
        {
            // usage: ingest <path> <dataset>
            var input = args.Length >= 3
               ? args[1]
               : ResolveSamplesDemoPath();

            var dataset = args.Length >= 3 ? args[2] : "DemoDataset";

            await ingestEntry.RunAsync(
    new DatasetIngestRequest(
        Dataset: dataset,
        Role: "refs",
        InputPath: input,
        Mode: DatasetIngestMode.Plain),
    textLineIngestor: txtLineIngestor);

            Console.WriteLine("Ingest finished.");
            break;
        }

    case "eval":
        {
            // usage:
            //   eval <dataset>                 -> load persisted embeddings from FileStore
            //   eval <dataset> --sim           -> use simulated embeddings (old behavior)
            //   eval <dataset> --baseline      -> compare against identity baseline (shift vs baseline metrics)
            var dataset = args.Length >= 2 ? args[1] : "DemoDataset";
            var useSim = args.Any(a => string.Equals(a, "--sim", StringComparison.OrdinalIgnoreCase));
            var useBaseline = args.Any(a => string.Equals(a, "--baseline", StringComparison.OrdinalIgnoreCase));

            // eval-only options
            var shiftArg = args.FirstOrDefault(a => a.StartsWith("--shift=", StringComparison.OrdinalIgnoreCase))
                ?.Substring("--shift=".Length)
                ?.Trim();

            var gateEps = 1e-6;
            var gateEpsArg = args.FirstOrDefault(a => a.StartsWith("--gate-eps=", StringComparison.OrdinalIgnoreCase))
                ?.Substring("--gate-eps=".Length)
                ?.Trim();

            if (!string.IsNullOrWhiteSpace(gateEpsArg))
            {
                if (double.TryParse(gateEpsArg, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    gateEps = parsed;
            }

            // Shift selection for eval (kept intentionally minimal)
            IShift shift = (shiftArg ?? "identity").ToLowerInvariant() switch
            {
                "zero" => new EmbeddingShift.Core.Shifts.MultiplicativeShift(0f, EmbeddingDimensions.DIM),
                _ => new NullShift()
            };

            var res = await evalEntry.RunAsync(
                shift,
                new DatasetEvalRequest(dataset, UseSim: useSim, UseBaseline: useBaseline));

            if (!string.IsNullOrWhiteSpace(res.ModeLine))
                Console.WriteLine(res.ModeLine);

            if (!res.DidRun)
            {
                Console.WriteLine(res.Notes);
                return;
            }

            // Acceptance gate is meaningful only in baseline mode (needs *.delta metrics)
            if (useBaseline)
            {

                var gateProfile = args.FirstOrDefault(a => a.StartsWith("--gate-profile=", StringComparison.OrdinalIgnoreCase))
                    ?.Substring("--gate-profile=".Length)
                    ?.Trim();

                var gate = EvalAcceptanceGate.CreateFromProfile(gateProfile, gateEps);
                var gateRes = gate.Evaluate(res.Metrics);

                Console.WriteLine($"Acceptance gate: {(gateRes.Passed ? "PASS" : "FAIL")} (eps={gateRes.Epsilon:G}).");
                foreach (var note in gateRes.Notes)
                    Console.WriteLine(note);

                if (!gateRes.Passed)
                {
                    Environment.ExitCode = 2;
                    return;
                }
            }

            break;
        }
    case "run":
        {
            // usage:
            //   run <refsPath> <queriesPath> <dataset> [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive] [--sim] [--baseline]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: run <refsPath> <queriesPath> <dataset> [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive] [--sim] [--baseline] [--shift=identity|zero] [--gate-profile=rank|rank+cosine] [--gate-eps=1e-6]");
                Environment.ExitCode = 1;
                return;
            }

            var refsPath = args[1];
            var queriesPath = args[2];
            var dataset = args[3];

            var refsMode = args.Any(a => string.Equals(a, "--refs-plain", StringComparison.OrdinalIgnoreCase))
                ? DatasetIngestMode.Plain
                : DatasetIngestMode.ChunkFirst;

            var chunkSize = 1000;
            var chunkOverlap = 100;
            var recursive = true;
            var useSim = args.Any(a => string.Equals(a, "--sim", StringComparison.OrdinalIgnoreCase));
            var useBaseline = args.Any(a => string.Equals(a, "--baseline", StringComparison.OrdinalIgnoreCase));

            foreach (var a in args)
            {
                if (a.StartsWith("--chunk-size=", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(a.Substring("--chunk-size=".Length), out var cs) && cs > 0)
                    chunkSize = cs;

                if (a.StartsWith("--chunk-overlap=", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(a.Substring("--chunk-overlap=".Length), out var co) && co >= 0)
                    chunkOverlap = co;

                if (a.Equals("--no-recursive", StringComparison.OrdinalIgnoreCase))
                    recursive = false;
            }

            var shiftArg = args.FirstOrDefault(a => a.StartsWith("--shift=", StringComparison.OrdinalIgnoreCase))
                ?.Substring("--shift=".Length)
                ?.Trim();

            // Shift selection for eval (kept intentionally minimal)
            IShift shift = (shiftArg ?? "identity").ToLowerInvariant() switch
            {
                "zero" => new EmbeddingShift.Core.Shifts.MultiplicativeShift(0f, EmbeddingDimensions.DIM),
                _ => new NullShift()
            };

            var res = await runEntry.RunAsync(
                shift,
                new DatasetRunRequest(
                    Dataset: dataset,
                    RefsPath: refsPath,
                    QueriesPath: queriesPath,
                    RefsMode: refsMode,
                    ChunkSize: chunkSize,
                    ChunkOverlap: chunkOverlap,
                    Recursive: recursive,
                    EvalUseSim: useSim,
                    EvalUseBaseline: useBaseline),
                txtLineIngestor,
                queriesJsonIngestor);

            if (res.RefsIngest.Mode == DatasetIngestMode.ChunkFirst && !string.IsNullOrWhiteSpace(res.RefsIngest.ManifestPath))
                Console.WriteLine($"Refs manifest: {res.RefsIngest.ManifestPath}");

            if (!string.IsNullOrWhiteSpace(res.EvalResult.ModeLine))
                Console.WriteLine(res.EvalResult.ModeLine);

            if (!res.EvalResult.DidRun && !string.IsNullOrWhiteSpace(res.EvalResult.Notes))
                Console.WriteLine(res.EvalResult.Notes);

            if (useBaseline && res.EvalResult.DidRun)
            {
                var gateEps = 1e-6;
                var gateEpsArg = args.FirstOrDefault(a => a.StartsWith("--gate-eps=", StringComparison.OrdinalIgnoreCase))
                    ?.Substring("--gate-eps=".Length)
                    ?.Trim();

                if (!string.IsNullOrWhiteSpace(gateEpsArg))
                {
                    if (double.TryParse(gateEpsArg, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                        gateEps = parsed;
                }

                var gateProfile = args.FirstOrDefault(a => a.StartsWith("--gate-profile=", StringComparison.OrdinalIgnoreCase))
                    ?.Substring("--gate-profile=".Length)
                    ?.Trim();

                var gate = EvalAcceptanceGate.CreateFromProfile(gateProfile, gateEps);
                var gateRes = gate.Evaluate(res.EvalResult.Metrics);

                Console.WriteLine($"Acceptance gate: {(gateRes.Passed ? "PASS" : "FAIL")} (eps={gateRes.Epsilon:G}).");
                foreach (var note in gateRes.Notes)
                    Console.WriteLine(note);

                if (!gateRes.Passed)
                {
                    Environment.ExitCode = 2;
                }
            }

            break;
        }


    case "run-demo":
        {
            // usage:
            //   run-demo [<dataset>] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive] [--sim] [--baseline]
            var dataset = "DemoDataset";
            var argi = 1;

            if (args.Length >= 2 && !args[1].StartsWith("--", StringComparison.Ordinal))
            {
                dataset = args[1];
                argi = 2;
            }

            var chunkSize = 900;
            var chunkOverlap = 120;
            var recursive = true;
            var useSim = args.Any(a => string.Equals(a, "--sim", StringComparison.OrdinalIgnoreCase));
            var useBaseline = args.Any(a => string.Equals(a, "--baseline", StringComparison.OrdinalIgnoreCase));

            for (var i = argi; i < args.Length; i++)
            {
                var a = args[i];

                if (a.StartsWith("--chunk-size=", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(a.Substring("--chunk-size=".Length), out var cs) && cs > 0)
                    chunkSize = cs;

                if (a.StartsWith("--chunk-overlap=", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(a.Substring("--chunk-overlap=".Length), out var co) && co >= 0)
                    chunkOverlap = co;

                if (a.Equals("--no-recursive", StringComparison.OrdinalIgnoreCase))
                    recursive = false;
            }

            var dataRoot = DirectoryLayout.ResolveDataRoot();
            var repoRoot = Path.GetFullPath(Path.Combine(dataRoot, ".."));

            if (RepositoryLayout.TryResolveRepoRoot(out var rr))
                repoRoot = rr;

            var refsPath = Path.Combine(repoRoot, "samples", "insurance", "policies");
            var queriesPath = Path.Combine(repoRoot, "samples", "insurance", "queries");

            var shiftArg = args.FirstOrDefault(a => a.StartsWith("--shift=", StringComparison.OrdinalIgnoreCase))
                ?.Substring("--shift=".Length)
                ?.Trim();

            // Shift selection for eval (kept intentionally minimal)
            IShift shift = (shiftArg ?? "identity").ToLowerInvariant() switch
            {
                "zero" => new EmbeddingShift.Core.Shifts.MultiplicativeShift(0f, EmbeddingDimensions.DIM),
                _ => new NullShift()
            };

            var res = await runEntry.RunAsync(
                shift,
                new DatasetRunRequest(
                    Dataset: dataset,
                    RefsPath: refsPath,
                    QueriesPath: queriesPath,
                    RefsMode: DatasetIngestMode.ChunkFirst,
                    ChunkSize: chunkSize,
                    ChunkOverlap: chunkOverlap,
                    Recursive: recursive,
                    EvalUseSim: useSim,
                    EvalUseBaseline: useBaseline),
                txtLineIngestor,
                queriesJsonIngestor);

            if (res.RefsIngest.Mode == DatasetIngestMode.ChunkFirst && !string.IsNullOrWhiteSpace(res.RefsIngest.ManifestPath))
                Console.WriteLine($"Refs manifest: {res.RefsIngest.ManifestPath}");

            if (!string.IsNullOrWhiteSpace(res.EvalResult.ModeLine))
                Console.WriteLine(res.EvalResult.ModeLine);

            if (!res.EvalResult.DidRun && !string.IsNullOrWhiteSpace(res.EvalResult.Notes))
                Console.WriteLine(res.EvalResult.Notes);

            if (useBaseline && res.EvalResult.DidRun)
            {
                var gateEps = 1e-6;
                var gateEpsArg = args.FirstOrDefault(a => a.StartsWith("--gate-eps=", StringComparison.OrdinalIgnoreCase))
                    ?.Substring("--gate-eps=".Length)
                    ?.Trim();

                if (!string.IsNullOrWhiteSpace(gateEpsArg))
                {
                    if (double.TryParse(gateEpsArg, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                        gateEps = parsed;
                }

                var gateProfile = args.FirstOrDefault(a => a.StartsWith("--gate-profile=", StringComparison.OrdinalIgnoreCase))
                    ?.Substring("--gate-profile=".Length)
                    ?.Trim();

                var gate = EvalAcceptanceGate.CreateFromProfile(gateProfile, gateEps);
                var gateRes = gate.Evaluate(res.EvalResult.Metrics);

                Console.WriteLine($"Acceptance gate: {(gateRes.Passed ? "PASS" : "FAIL")} (eps={gateRes.Epsilon:G}).");
                foreach (var note in gateRes.Notes)
                    Console.WriteLine(note);

                if (!gateRes.Passed)
                {
                    Environment.ExitCode = 2;
                }
            }

            break;
        }

    case "ingest-queries":
        {
            // usage: ingest-queries <path> <dataset>
            var input = args.Length >= 3
                ? args[1]
                : ResolveSamplesDemoPath();

            var dataset = args.Length >= 3 ? args[2] : "DemoDataset";

            await ingestEntry.RunAsync(
                new DatasetIngestRequest(
                    Dataset: dataset,
                    Role: "queries",
                    InputPath: input,
                    Mode: DatasetIngestMode.Plain),
                textLineIngestor: txtLineIngestor,
                queriesJsonIngestor: queriesJsonIngestor);

            Console.WriteLine("Ingest (queries) finished.");
            break;
        }

    case "ingest-refs":
        {
            // usage: ingest-refs <path> <dataset>
            var input = args.Length >= 3
              ? args[1]
              : ResolveSamplesDemoPath();

            var dataset = args.Length >= 3 ? args[2] : "DemoDataset";

            await ingestEntry.RunAsync(
                new DatasetIngestRequest(
                    Dataset: dataset,
                    Role: "refs",
                    InputPath: input,
                    Mode: DatasetIngestMode.Plain),
                textLineIngestor: txtLineIngestor);

            Console.WriteLine("Ingest (refs) finished.");
            break;
        }


    case "ingest-refs-chunked":
        {
            // usage: ingest-refs-chunked <path> <dataset> [--chunk-size=1000] [--chunk-overlap=100] [--no-recursive]
            var input = args.Length >= 3
                ? args[1]
                : ResolveSamplesDemoPath();

            var dataset = args.Length >= 3 ? args[2] : "DemoDataset";

            var chunkSize = 1000;
            var chunkOverlap = 100;
            var recursive = true;

            foreach (var a in args)
            {
                if (a.StartsWith("--chunk-size=", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(a.Substring("--chunk-size=".Length), out var cs) && cs > 0)
                    chunkSize = cs;

                if (a.StartsWith("--chunk-overlap=", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(a.Substring("--chunk-overlap=".Length), out var co) && co >= 0)
                    chunkOverlap = co;

                if (a.Equals("--no-recursive", StringComparison.OrdinalIgnoreCase))
                    recursive = false;
            }

            var result = await ingestEntry.RunAsync(
                new DatasetIngestRequest(
                    Dataset: dataset,
                    Role: "refs",
                    InputPath: input,
                    Mode: DatasetIngestMode.ChunkFirst,
                    ChunkSize: chunkSize,
                    ChunkOverlap: chunkOverlap,
                    Recursive: recursive),
                textLineIngestor: txtLineIngestor);

            Console.WriteLine($"Ingest (refs, chunked) finished. Manifest: {result.ManifestPath}");
            break;
        }


    case "adaptive":
        {
            // Default workflow/domain for convenience.
            var workflowName = "mini-insurance-posneg";
            var domainKey = "insurance";

            // Optional arguments:
            //   adaptive [workflowName] [domainKey] [--baseline]
            // Flags (like --baseline) are ignored here and handled separately.
            if (args.Length > 1)
            {
                var position = 0;

                for (var i = 1; i < args.Length; i++)
                {
                    var token = args[i];

                    if (string.IsNullOrWhiteSpace(token))
                        continue;

                    // Keep compatibility with flag-style arguments (e.g. --baseline).
                    if (token.StartsWith("-", StringComparison.Ordinal))
                        continue;

                    if (position == 0)
                    {
                        workflowName = token;
                    }
                    else if (position == 1)
                    {
                        domainKey = token;
                    }

                    position++;
                }
            }

            // Use a training-backed shift generator that reads the latest
            // shift training result and exposes it as a learned additive shift.
            var resultsRoot = DirectoryLayout.ResolveResultsRoot(domainKey);
            var repository =
                new EmbeddingShift.ConsoleEval.Repositories.FileSystemShiftTrainingResultRepository(resultsRoot);

            IShiftGenerator generator = new TrainingBackedShiftGenerator(
                repository,
                workflowName: workflowName);

            var service = new ShiftEvaluationService(generator, EvaluatorCatalog.Defaults);

            var wf = new AdaptiveWorkflow(generator, service, method);

            Console.WriteLine(
                $"Adaptive ready (method={method}, workflow={workflowName}, domain={domainKey}).");

            AdaptiveDemo.RunDemo(wf);
            // Example usage (later):
            // var best = wf.Run(queries[0], refs);

            break;
        }

    case "mini-insurance-adaptive":
    case "mini-insurance-adaptive-demo":
        {
            // Alias for the adaptive command with implicit Mini-Insurance defaults.
            // We simply jump to the "adaptive" case; workflow/domain defaults
            // are already set to mini-insurance-posneg / insurance there.
            goto case "adaptive";
        }

    case "--help":
    case "-h":
        {
            Helpers.PrintHelp();
            break;
        }

    case "mini-insurance":
        {
            Console.WriteLine("[MiniInsurance] Running file-based insurance workflow using sample policies and queries...");

            // Workflow instance as in the tests.
            IWorkflow workflow = new FileBasedInsuranceMiniWorkflow();
            var wfRunner = new StatsAwareWorkflowRunner();

            // Runs like in FileBasedInsuranceMiniWorkflowTests, just under a different run name.
            var result = await wfRunner.ExecuteAsync("FileBased-Insurance-Mini-Console", workflow);

            // Use the central layout helper: /results/insurance (with safe fallbacks).
            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");

            string? persistedPath = null;

            try
            {
                persistedPath = await RunPersistor.Persist(baseDir, result);
                Console.WriteLine($"[MiniInsurance] Results persisted to: {persistedPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MiniInsurance] WARNING: Failed to persist results under '{baseDir}': {ex.Message}");
            }

            if (persistedPath is null)
            {
                Console.WriteLine("[MiniInsurance] WARNING: Could not persist results.");
            }

            Console.WriteLine();
            Console.WriteLine(result.ReportMarkdown("Mini Insurance Evaluation"));
            break;
        }
    case "mini-insurance-first-delta":
        {
            Console.WriteLine("[MiniInsurance] Baseline vs FirstShift vs First+Delta (mini workflow)");
            Console.WriteLine();

            var wfRunner = new StatsAwareWorkflowRunner();

            // Baseline: default pipeline (no shifts)
            IWorkflow baselineWorkflow = new FileBasedInsuranceMiniWorkflow();
            var baselineResult = await wfRunner.ExecuteAsync(
                "FileBased-Insurance-Mini-Baseline",
                baselineWorkflow);

            if (!baselineResult.Success)
            {
                Console.WriteLine("[MiniInsurance] Baseline run failed:");
                Console.WriteLine(baselineResult.ReportMarkdown("Mini Insurance Baseline"));
                break;
            }

            // FirstShift only
            var firstPipeline = FileBasedInsuranceMiniWorkflow.CreateFirstShiftPipeline();
            IWorkflow firstWorkflow = new FileBasedInsuranceMiniWorkflow(firstPipeline);
            var firstResult = await wfRunner.ExecuteAsync(
                "FileBased-Insurance-Mini-FirstShift",
                firstWorkflow);

            if (!firstResult.Success)
            {
                Console.WriteLine("[MiniInsurance] FirstShift run failed:");
                Console.WriteLine(firstResult.ReportMarkdown("Mini Insurance FirstShift"));
                break;
            }

            // First + Delta
            var firstDeltaPipeline = FileBasedInsuranceMiniWorkflow.CreateFirstPlusDeltaPipeline();
            IWorkflow firstDeltaWorkflow = new FileBasedInsuranceMiniWorkflow(firstDeltaPipeline);
            var firstDeltaResult = await wfRunner.ExecuteAsync(
                "FileBased-Insurance-Mini-FirstPlusDelta",
                firstDeltaWorkflow);

            if (!firstDeltaResult.Success)
            {
                Console.WriteLine("[MiniInsurance] First+Delta run failed:");
                Console.WriteLine(firstDeltaResult.ReportMarkdown("Mini Insurance First+Delta"));
                break;
            }

            // Persist individual runs under /results/insurance and build a JSON+Markdown comparison.
            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");

            string? baselineRunDir = null;
            string? firstRunDir = null;
            string? firstPlusDeltaRunDir = null;
            string? comparisonDir = null;

            try
            {
                baselineRunDir = await RunPersistor.Persist(baseDir, baselineResult);
                firstRunDir = await RunPersistor.Persist(baseDir, firstResult);
                firstPlusDeltaRunDir = await RunPersistor.Persist(baseDir, firstDeltaResult);

                var comparison = MiniInsuranceFirstDeltaArtifacts.CreateComparison(
                    baselineResult,
                    firstResult,
                    firstDeltaResult,
                    baselineRunDir,
                    firstRunDir,
                    firstPlusDeltaRunDir);

                comparisonDir = MiniInsuranceFirstDeltaArtifacts.PersistComparison(baseDir, comparison);

                Console.WriteLine($"[MiniInsurance] Baseline run persisted to:      {baselineRunDir}");
                Console.WriteLine($"[MiniInsurance] FirstShift run persisted to:   {firstRunDir}");
                Console.WriteLine($"[MiniInsurance] First+Delta run persisted to: {firstPlusDeltaRunDir}");
                Console.WriteLine($"[MiniInsurance] Metrics comparison persisted to: {comparisonDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MiniInsurance] WARNING: Failed to persist First/Delta artifacts under '{baseDir}': {ex.Message}");
            }

            var baselineMetrics = baselineResult.Metrics ?? new System.Collections.Generic.Dictionary<string, double>();
            var firstMetrics = firstResult.Metrics ?? new System.Collections.Generic.Dictionary<string, double>();
            var firstDeltaMetrics = firstDeltaResult.Metrics ?? new System.Collections.Generic.Dictionary<string, double>();

            var allKeys = new System.Collections.Generic.SortedSet<string>(baselineMetrics.Keys);
            allKeys.UnionWith(firstMetrics.Keys);
            allKeys.UnionWith(firstDeltaMetrics.Keys);

            Console.WriteLine();
            Console.WriteLine("[MiniInsurance] Metrics comparison (Baseline vs First vs First+Delta):");
            Console.WriteLine();
            Console.WriteLine("Metric                Baseline    First      First+Delta   ΔFirst-BL   ΔFirst+Delta-BL");
            Console.WriteLine("-------------------   --------    --------   -----------   ---------   ---------------");

            foreach (var key in allKeys)
            {
                baselineMetrics.TryGetValue(key, out var b);
                firstMetrics.TryGetValue(key, out var f);
                firstDeltaMetrics.TryGetValue(key, out var fd);

                var df = f - b;
                var dfd = fd - b;

                Console.WriteLine(
                    $"{key,-19}   {b,8:F3}    {f,8:F3}   {fd,11:F3}   {df,9:+0.000;-0.000;0.000}   {dfd,15:+0.000;-0.000;0.000}");
            }

            Console.WriteLine();
            Console.WriteLine("[MiniInsurance] Done.");
            break;
        }

    case "mini-insurance-first-delta-pipeline":
        {
            // Legacy alias (kept for compatibility).
            // Prefer: domain mini-insurance pipeline [--no-learned]
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
        // Legacy alias (kept for compatibility).
        // Prefer: domain mini-insurance training-inspect
        await ExecuteDomainPackAsync(
            "mini-insurance",
            new[] { "training-inspect" }.Concat(args.Skip(1)).ToArray());
        break;

    case "mini-insurance-training-list":
        // Legacy alias (kept for compatibility).
        // Prefer: domain mini-insurance training-list
        await ExecuteDomainPackAsync(
            "mini-insurance",
            new[] { "training-list" }.Concat(args.Skip(1)).ToArray());
        break;

    case "mini-insurance-first-delta-aggregate":
        {
            Console.WriteLine("[MiniInsurance] Aggregating First/Delta metrics from previous comparison runs...");
            Console.WriteLine();

            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");

            try
            {
                var aggregate = MiniInsuranceFirstDeltaAggregator.AggregateFromDirectory(baseDir);
                var aggregateDir = MiniInsuranceFirstDeltaAggregator.PersistAggregate(baseDir, aggregate);

                Console.WriteLine($"[MiniInsurance] Aggregated {aggregate.ComparisonCount} comparison runs.");
                Console.WriteLine($"[MiniInsurance] Aggregate metrics persisted to: {aggregateDir}");
                Console.WriteLine();

                Console.WriteLine("Metric                AvgBaseline   AvgFirst    AvgFirst+Delta   AvgΔFirst-BL   AvgΔFirst+Delta-BL");
                Console.WriteLine("-------------------   -----------   ---------   --------------   ------------   -------------------");

                foreach (var row in aggregate.Metrics)
                {
                    Console.WriteLine(
                        $"{row.Metric,-19}   {row.AverageBaseline,11:0.000}   {row.AverageFirst,9:0.000}   {row.AverageFirstPlusDelta,14:0.000}   {row.AverageDeltaFirstVsBaseline,12:+0.000;-0.000;0.000}   {row.AverageDeltaFirstPlusDeltaVsBaseline,19:+0.000;-0.000;0.000}");
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MiniInsurance] ERROR while aggregating metrics: {ex.Message}");
            }

            Console.WriteLine("[MiniInsurance] Aggregation done.");
            break;
        }

    case "shift-training-inspect":
        {
            // Generic inspector for file-based shift training results under /results/<domainKey>.
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
            Console.WriteLine("[MiniInsurance] Training Delta shift candidate from aggregated First/Delta metrics...");
            Console.WriteLine();

            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");

            try
            {
                var aggregate = MiniInsuranceFirstDeltaAggregator.AggregateFromDirectory(baseDir);
                var candidate = MiniInsuranceFirstDeltaTrainer.TrainFromAggregate(baseDir, aggregate);
                var trainingDir = MiniInsuranceFirstDeltaTrainer.PersistCandidate(baseDir, candidate);

                Console.WriteLine($"[MiniInsurance] Used {aggregate.ComparisonCount} comparison runs.");
                Console.WriteLine($"[MiniInsurance] Training artifacts persisted to: {trainingDir}");
                Console.WriteLine();

                Console.WriteLine($"Combined First improvement:       {candidate.ImprovementFirst:+0.000;-0.000;0.000}");
                Console.WriteLine($"Combined First+Delta improvement: {candidate.ImprovementFirstPlusDelta:+0.000;-0.000;0.000}");
                Console.WriteLine($"Delta improvement vs First:       {candidate.DeltaImprovement:+0.000;-0.000;0.000}");
                Console.WriteLine();
                Console.WriteLine("Proposed Delta vector (index: value):");

                for (int i = 0; i < candidate.DeltaVector.Length; i++)
                {
                    Console.WriteLine($"  [{i}] = {candidate.DeltaVector[i]:+0.000;-0.000;0.000}");
                }

                Console.WriteLine();
                Console.WriteLine("[MiniInsurance] Training done.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MiniInsurance] ERROR: Training failed under '{baseDir}': {ex.Message}");
            }

            break;
        }
    case "mini-insurance-first-learned-delta":
        {
            Console.WriteLine("[MiniInsurance] Baseline vs FirstShift vs First+LearnedDelta (mini workflow)");
            Console.WriteLine();

            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");

            // Load latest trained Delta candidate.
            var learnedDelta = MiniInsuranceFirstDeltaCandidateLoader
                .LoadLatestDeltaVectorOrDefault(baseDir, out var found);

            if (!found)
            {
                Console.WriteLine("[MiniInsurance] No trained Delta candidate found.");
                Console.WriteLine($"  Looked under: {baseDir}");
                Console.WriteLine("  Run 'mini-insurance-first-delta', then");
                Console.WriteLine("      'mini-insurance-first-delta-aggregate' and");
                Console.WriteLine("      'mini-insurance-first-delta-train' first.");
                break;
            }

            var wfRunner = new StatsAwareWorkflowRunner();

            // Baseline: default pipeline (no shifts).
            IWorkflow baselineWorkflow = new FileBasedInsuranceMiniWorkflow();
            var baselineResult = await wfRunner.ExecuteAsync(
                "FileBased-Insurance-Mini-Baseline-Learned",
                baselineWorkflow);

            if (!baselineResult.Success)
            {
                Console.WriteLine("[MiniInsurance] Baseline run failed:");
                Console.WriteLine(baselineResult.ReportMarkdown("Mini Insurance Baseline (LearnedDelta)"));
                break;
            }

            // FirstShift only.
            var firstPipeline = FileBasedInsuranceMiniWorkflow.CreateFirstShiftPipeline();
            IWorkflow firstWorkflow = new FileBasedInsuranceMiniWorkflow(firstPipeline);
            var firstResult = await wfRunner.ExecuteAsync(
                "FileBased-Insurance-Mini-FirstShift-Learned",
                firstWorkflow);

            if (!firstResult.Success)
            {
                Console.WriteLine("[MiniInsurance] FirstShift run failed:");
                Console.WriteLine(firstResult.ReportMarkdown("Mini Insurance FirstShift (LearnedDelta)"));
                break;
            }

            // First + learned Delta.
            var learnedPipeline = FileBasedInsuranceMiniWorkflow.CreateFirstPlusDeltaPipeline(learnedDelta);
            IWorkflow learnedWorkflow = new FileBasedInsuranceMiniWorkflow(learnedPipeline);
            var learnedResult = await wfRunner.ExecuteAsync(
                "FileBased-Insurance-Mini-FirstPlusLearnedDelta",
                learnedWorkflow);

            if (!learnedResult.Success)
            {
                Console.WriteLine("[MiniInsurance] First+LearnedDelta run failed:");
                Console.WriteLine(learnedResult.ReportMarkdown("Mini Insurance First+LearnedDelta"));
                break;
            }

            // Persist runs + comparison so they can be picked up again by the aggregation step.
            string? baselineRunDir = null;
            string? firstRunDir = null;
            string? learnedRunDir = null;
            string? comparisonDir = null;

            try
            {
                baselineRunDir = await RunPersistor.Persist(baseDir, baselineResult);
                firstRunDir = await RunPersistor.Persist(baseDir, firstResult);
                learnedRunDir = await RunPersistor.Persist(baseDir, learnedResult);

                var comparison = MiniInsuranceFirstDeltaArtifacts.CreateComparison(
                    baselineResult,
                    firstResult,
                    learnedResult,
                    baselineRunDir,
                    firstRunDir,
                    learnedRunDir);

                comparisonDir = MiniInsuranceFirstDeltaArtifacts.PersistComparison(baseDir, comparison);

                Console.WriteLine($"[MiniInsurance] Baseline run persisted to:           {baselineRunDir}");
                Console.WriteLine($"[MiniInsurance] FirstShift run persisted to:        {firstRunDir}");
                Console.WriteLine($"[MiniInsurance] First+LearnedDelta run persisted to:{learnedRunDir}");
                Console.WriteLine($"[MiniInsurance] Metrics comparison persisted to:    {comparisonDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MiniInsurance] WARNING: Failed to persist LearnedDelta artifacts under '{baseDir}': {ex.Message}");
            }

            var baselineMetrics = baselineResult.Metrics ?? new System.Collections.Generic.Dictionary<string, double>();
            var firstMetrics = firstResult.Metrics ?? new System.Collections.Generic.Dictionary<string, double>();
            var learnedMetrics = learnedResult.Metrics ?? new System.Collections.Generic.Dictionary<string, double>();

            var allKeys = new System.Collections.Generic.SortedSet<string>(baselineMetrics.Keys);
            allKeys.UnionWith(firstMetrics.Keys);
            allKeys.UnionWith(learnedMetrics.Keys);

            Console.WriteLine();
            Console.WriteLine("[MiniInsurance] Metrics comparison (Baseline vs First vs First+LearnedDelta):");
            Console.WriteLine();
            Console.WriteLine("Metric                Baseline    First      First+LearnedΔ   ΔFirst-BL   ΔFirst+LearnedΔ-BL");
            Console.WriteLine("-------------------   --------    --------   --------------   ---------   -------------------");

            foreach (var key in allKeys)
            {
                baselineMetrics.TryGetValue(key, out var b);
                firstMetrics.TryGetValue(key, out var f);
                learnedMetrics.TryGetValue(key, out var fl);

                var df = f - b;
                var dfl = fl - b;

                Console.WriteLine(
                    $"{key,-19}   {b,8:F3}    {f,8:F3}   {fl,14:F3}   {df,9:+0.000;-0.000;0.000}   {dfl,19:+0.000;-0.000;0.000}");
            }

            Console.WriteLine();
            Console.WriteLine("[MiniInsurance] LearnedDelta comparison done.");
            break;
        }

    case "mini-insurance-first-delta-inspect":
        {
            Console.WriteLine("[MiniInsurance] Inspecting latest trained Delta candidate...");
            Console.WriteLine("  mini-insurance-shift-training-inspect  inspect latest generic shift training result for mini-insurance");
            Console.WriteLine("  mini-insurance-shift-training-history  list recent generic shift training results for mini-insurance");
            Console.WriteLine();

            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");
            var candidate = MiniInsuranceFirstDeltaCandidateLoader
                .LoadLatestCandidate(baseDir, out var found);

            if (!found || candidate == null)
            {
                Console.WriteLine("[MiniInsurance] No shift candidate found.");
                Console.WriteLine($"  Looked under: {baseDir}");
                Console.WriteLine("  Run 'mini-insurance-first-delta-aggregate' and");
                Console.WriteLine("      'mini-insurance-first-delta-train' first.");
                break;
            }

            Console.WriteLine($"Created (UTC):            {candidate.CreatedUtc:O}");
            Console.WriteLine($"Base directory:           {candidate.BaseDirectory}");
            Console.WriteLine($"Comparison runs:          {candidate.ComparisonRuns}");
            Console.WriteLine($"Improvement First:        {candidate.ImprovementFirst:+0.000;-0.000;0.000}");
            Console.WriteLine($"Improvement First+Delta:  {candidate.ImprovementFirstPlusDelta:+0.000;-0.000;0.000}");
            Console.WriteLine($"Delta improvement vs First: {candidate.DeltaImprovement:+0.000;-0.000;0.000}");
            Console.WriteLine();

            var vector = candidate.DeltaVector ?? Array.Empty<float>();
            if (vector.Length == 0)
            {
                Console.WriteLine("Delta vector: (empty)");
                Console.WriteLine();
                Console.WriteLine("[MiniInsurance] Candidate inspection done.");
                break;
            }

            Console.WriteLine("Top Delta dimensions (by |value|):");

            var used = new bool[vector.Length];
            const int topN = 8;

            for (int n = 0; n < topN; n++)
            {
                var bestIdx = -1;
                var bestAbs = 0.0f;

                for (int i = 0; i < vector.Length; i++)
                {
                    if (used[i]) continue;
                    var abs = Math.Abs(vector[i]);
                    if (abs > bestAbs)
                    {
                        bestAbs = abs;
                        bestIdx = i;
                    }
                }

                if (bestIdx < 0 || bestAbs <= 0.0f)
                {
                    break;
                }

                used[bestIdx] = true;
                Console.WriteLine($"  [{bestIdx}] = {vector[bestIdx]:+0.000;-0.000;0.000}");
            }

            Console.WriteLine();
            Console.WriteLine("[MiniInsurance] Candidate inspection done.");
            break;
        }

    case "mini-insurance-shift-training-inspect":
        // Legacy alias (kept for compatibility).
        // Prefer: domain mini-insurance shift-training-inspect
        await ExecuteDomainPackAsync(
            "mini-insurance",
            new[] { "shift-training-inspect" }.Concat(args.Skip(1)).ToArray());
        break;

    case "mini-insurance-shift-training-history":
        // Legacy alias (kept for compatibility).
        // Prefer: domain mini-insurance shift-training-history
        await ExecuteDomainPackAsync(
            "mini-insurance",
            new[] { "shift-training-history" }.Concat(args.Skip(1)).ToArray());
        break;

    case "mini-insurance-shift-training-best":
        // Legacy alias (kept for compatibility).
        // Prefer: domain mini-insurance shift-training-best
        await ExecuteDomainPackAsync(
            "mini-insurance",
            new[] { "shift-training-best" }.Concat(args.Skip(1)).ToArray());
        break;

    case "mini-insurance-posneg-train":
        // Legacy alias (kept for compatibility).
        // Prefer: domain mini-insurance posneg-train
        await ExecuteDomainPackAsync(
            "mini-insurance",
            new[] { "posneg-train" }.Concat(args.Skip(1)).ToArray());
        break;

    case "mini-insurance-posneg-training-inspect":
        // Legacy alias (kept for compatibility).
        // Prefer: domain mini-insurance posneg-inspect
        await ExecuteDomainPackAsync(
            "mini-insurance",
            new[] { "posneg-inspect" }.Concat(args.Skip(1)).ToArray());
        break;

    case "mini-insurance-posneg-training-history":
        // Legacy alias (kept for compatibility).
        // Prefer: domain mini-insurance posneg-history
        await ExecuteDomainPackAsync(
            "mini-insurance",
            new[] { "posneg-history" }.Concat(args.Skip(1)).ToArray());
        break;

    case "mini-insurance-posneg-training-best":
        // Legacy alias (kept for compatibility).
        // Prefer: domain mini-insurance posneg-best
        await ExecuteDomainPackAsync(
            "mini-insurance",
            new[] { "posneg-best" }.Concat(args.Skip(1)).ToArray());
        break;

    case "mini-insurance-posneg-run":
        // Legacy alias (kept for compatibility).
        // Prefer: domain mini-insurance posneg-run
        await ExecuteDomainPackAsync(
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
        Helpers.PrintHelp();
        Console.WriteLine("  adaptive [--baseline]     - adaptive shift selection (Baseline = identity)");
        break;
}

// --- Helper objects ---
sealed class NoopStore : IVectorStore
{
    public Task SaveEmbeddingAsync(Guid id, float[] vector, string space, string provider, int dimensions)
        => Task.CompletedTask;
    public Task<float[]> LoadEmbeddingAsync(Guid id)
        => Task.FromResult(Array.Empty<float>());
    public Task SaveShiftAsync(Guid id, string type, string parametersJson)
        => Task.CompletedTask;
    public Task<IEnumerable<(Guid id, string type, string parametersJson)>> LoadShiftsAsync()
        => Task.FromResult<IEnumerable<(Guid, string, string)>>(Array.Empty<(Guid, string, string)>());
    public Task SaveRunAsync(Guid runId, string kind, string dataset, DateTime startedAt, DateTime completedAt, string resultsPath)
        => Task.CompletedTask;
}

// NullShift = identity
sealed class NullShift : IShift
{
    public string Name => "NullShift";

    public ShiftKind Kind => ShiftKind.NoShift;

    public ReadOnlyMemory<float> Apply(ReadOnlySpan<float> input) => input.ToArray();
}

// --- Static helpers in class to avoid CS8803 ---
static class Helpers
{
    public static void PrintHelp()
    {
        Console.WriteLine("EmbeddingShift.ConsoleEval — usage");
        Console.WriteLine();
        Console.WriteLine("  --help | -h                         show this help");
        Console.WriteLine("  --version                           print version");
        Console.WriteLine("  domain list                         list available domain packs");
        Console.WriteLine("  domain <id> <subcommand>            run a domain pack command");
        Console.WriteLine("  demo --shift <Name> [--dataset X]   run tiny demo (e.g., NoShift.IngestBased)");
        Console.WriteLine("  ingest-queries <path> <dataset>     ingest query vectors (supports queries.json or *.txt)");
        Console.WriteLine("  ingest-refs-chunked <path> <dataset> [--chunk-size=N] [--chunk-overlap=N] [--no-recursive]  chunk-first refs ingest");
        Console.WriteLine("  eval <dataset> [--sim] [--baseline] [--shift=identity|zero] [--gate-profile=rank|rank+cosine] [--gate-eps=N]  evaluate persisted embeddings (baseline gate optional)");
        Console.WriteLine("  run <refsPath> <queriesPath> <dataset> [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive] [--sim] [--baseline]  ingest+eval flow");
        Console.WriteLine("  run-demo [<dataset>] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive] [--sim] [--baseline]  run sample insurance flow");
        Console.WriteLine("  ingest <path> <dataset>             alias for ingest-refs");
        Console.WriteLine("  adaptive [--baseline]               adaptive shift selection (baseline = identity)");
        Console.WriteLine("  mini-insurance-adaptive             adaptive selection for Mini-Insurance (alias for 'adaptive')");
        Console.WriteLine("  mini-insurance                      run mini insurance workflow (baseline)");
        Console.WriteLine("  mini-insurance-first-delta          compare baseline vs First/First+Delta (mini insurance)");
        Console.WriteLine("  mini-insurance-first-delta-pipeline run full Mini-Insurance First+Delta pipeline (baseline/First/First+Delta/LearnedDelta)");
        Console.WriteLine("  mini-insurance-first-delta-aggregate aggregate metrics over all comparison runs");
        Console.WriteLine("  mini-insurance-first-delta-train    train a Delta shift candidate from aggregated metrics");
        Console.WriteLine("  mini-insurance-first-delta-inspect  inspect latest trained Delta candidate");
        Console.WriteLine("  mini-insurance-first-learned-delta  compare baseline vs First vs First+LearnedDelta");
        Console.WriteLine("  mini-insurance-shift-training-history  list recent generic shift training results for mini-insurance");
        Console.WriteLine("  mini-insurance-shift-training-best     show best generic shift training result for mini-insurance");
        Console.WriteLine("  shift-training-inspect <workflowName> [domainKey]  inspect latest shift training result");
        Console.WriteLine("  shift-training-history <workflowName> [maxItems] [domainKey]  list recent shift training results");
        Console.WriteLine("  shift-training-best <workflowName> [domainKey]                show best shift training result");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-refs-chunked samples\\insurance\\policies DemoDataset --chunk-size=900 --chunk-overlap=120");
        Console.WriteLine("  dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-queries samples\\insurance\\queries DemoDataset");
        Console.WriteLine("  dotnet run --project src/EmbeddingShift.ConsoleEval -- eval DemoDataset");
        Console.WriteLine("  dotnet run --project src/EmbeddingShift.ConsoleEval -- demo --shift NoShift.IngestBased");
        Console.WriteLine("  dotnet run --project src/EmbeddingShift.ConsoleEval -- adaptive");
    }

    // in static class Helpers
    public static List<ReadOnlyMemory<float>> LoadVectorsBySpace(string space)
    {
        var root = DirectoryLayout.ResolveDataRoot("embeddings");

        var result = new List<ReadOnlyMemory<float>>();
        if (!Directory.Exists(root))
            return result;

        foreach (var file in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file);
                var rec = System.Text.Json.JsonSerializer.Deserialize<EmbeddingRec>(json);
                if (rec?.vector is null || rec.vector.Length == 0) continue;

                // Accept exact or substring match of the space (dataset)
                if (!string.IsNullOrWhiteSpace(rec.space) &&
                    (string.Equals(rec.space, space, StringComparison.OrdinalIgnoreCase) ||
                     rec.space.IndexOf(space, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    result.Add(rec.vector);
                }
            }
            catch
            {
                // ignore unreadable files
            }
        }

        return result;
    }
        public static void ClearEmbeddingsForSpace(string space)
    {
        var root = DirectoryLayout.ResolveDataRoot("embeddings");
        var logicalSpace = string.IsNullOrWhiteSpace(space) ? "default" : space.Trim();
        var spaceDir = Path.Combine(root, SpaceToPath(logicalSpace));

        if (Directory.Exists(spaceDir))
            Directory.Delete(spaceDir, recursive: true);
    }

    private static string SpaceToPath(string space)
    {
        var parts = space.Split(new[] { ':', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(SanitizePathPart);
        return Path.Combine(parts.ToArray());
    }

    private static string SanitizePathPart(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (invalid.Contains(chars[i])) chars[i] = '_';
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "default" : sanitized;
    }

    // Mirror of FileStore's record shape
    public sealed record EmbeddingRec(Guid id, string space, string provider, int dimensions, float[] vector);
}


