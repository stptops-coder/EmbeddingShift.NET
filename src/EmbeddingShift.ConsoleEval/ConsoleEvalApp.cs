using EmbeddingShift.Abstractions;           // ShiftMethod
using EmbeddingShift.Adaptive;               // ShiftEvaluationService
using EmbeddingShift.ConsoleEval;
using EmbeddingShift.ConsoleEval.Domains;
using EmbeddingShift.ConsoleEval.Commands;
using EmbeddingShift.Core.Evaluators;        // EvaluatorCatalog
using EmbeddingShift.Core.Infrastructure;    // DirectoryLayout for /data and /results roots
using EmbeddingShift.Core.Runs;        // RunPersistor
using EmbeddingShift.Core.Workflows;   // StatsAwareWorkflowRunner + ReportMarkdown
using EmbeddingShift.Workflows;              // AdaptiveWorkflow
using EmbeddingShift.Workflows.Ingest;       // DatasetIngestEntry (canonical ingest entrypoint)
using EmbeddingShift.Workflows.Run;
using EmbeddingShift.Workflows.Eval;
using System.Globalization;
namespace EmbeddingShift.ConsoleEval;

internal static class ConsoleEvalApp
{
    public static async Task<int> RunAsync(string[] args)
    {


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
        var ingestDatasetEntry = new DatasetIngestDatasetEntry(ingestEntry);
        var txtLineIngestor = new MinimalTxtIngestor();
        var queriesJsonIngestor = new JsonQueryIngestor();

        var evalWf = new EvaluationWorkflow(runner);
        var evalEntry = new DatasetEvalEntry(provider, evalWf);
        var runEntry = new DatasetRunEntry(ingestDatasetEntry, evalEntry);

        var services = new ConsoleEvalServices(
            Method: method,
            IngestEntry: ingestEntry,
            IngestDatasetEntry: ingestDatasetEntry,
            EvalEntry: evalEntry,
            RunEntry: runEntry,
            TxtLineIngestor: txtLineIngestor,
            QueriesJsonIngestor: queriesJsonIngestor);

        return await ConsoleEvalCli.RunAsync(args, services);

    }

    // --- Helper objects ---


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
}


