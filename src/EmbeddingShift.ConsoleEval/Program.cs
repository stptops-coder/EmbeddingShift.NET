using EmbeddingShift.Abstractions;           // ShiftMethod
using EmbeddingShift.Adaptive;               // ShiftEvaluationService
using EmbeddingShift.ConsoleEval;
using EmbeddingShift.ConsoleEval.Commands;
using EmbeddingShift.ConsoleEval.Inspector;
using EmbeddingShift.Core.Evaluators;        // EvaluatorCatalog
using EmbeddingShift.Core.Generators;        // DeltaShiftGenerator (example)
using EmbeddingShift.Core.Infrastructure;    // DirectoryLayout for /data and /results roots
using EmbeddingShift.Core.Runs;        // RunPersistor
using EmbeddingShift.Core.Stats;       // InMemoryStatsCollector
using EmbeddingShift.Core.Workflows;   // StatsAwareWorkflowRunner + ReportMarkdown
using EmbeddingShift.Workflows;              // AdaptiveWorkflow
using System.Linq;


// Composition Root (kept simple)
// Flags:
//   --provider=sim | openai-echo | openai-dryrun    (default: sim)
string providerArg = args.FirstOrDefault(a => a.StartsWith("--provider=", StringComparison.OrdinalIgnoreCase))
                    ?.Split('=', 2)[1] ?? "sim";

// CLI-level override of simulation environment options (if present)
string? simModeArg = null;
string? simNoiseArg = null;

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
}

if (!string.IsNullOrWhiteSpace(simModeArg))
{
    Environment.SetEnvironmentVariable("EMBEDDING_SIM_MODE", simModeArg);
}

if (!string.IsNullOrWhiteSpace(simNoiseArg))
{
    Environment.SetEnvironmentVariable("EMBEDDING_SIM_NOISE_AMPLITUDE", simNoiseArg);
}

var embeddingProvider = EmbeddingProviderFactory.FromEnvironment();
EmbeddingConsoleDiagnostics.PrintEmbeddingConfiguration();


// base provider used by all modes (still the existing SimEmbeddingProvider)
IEmbeddingProvider baseProvider = EmbeddingProviderFactory.FromEnvironment();
EmbeddingConsoleDiagnostics.PrintEmbeddingConfiguration();
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

// File-based vector store for persistence
IVectorStore store = new EmbeddingShift.Core.Persistence.FileStore(
    Path.Combine(AppContext.BaseDirectory, "data"));

var ingestWf = new IngestWorkflow(ingestor, provider, store);
var evalWf = new EvaluationWorkflow(runner);

// --- CLI ---
if (args.Length == 0)
{
    Helpers.PrintHelp();
    Console.WriteLine("  adaptive [--baseline]     - adaptive shift selection (Baseline = identity)");
    return;
}

switch (args[0].ToLowerInvariant())
{
    case "ingest":
        {
            // usage: ingest <path> <dataset>
            var input = args.Length >= 3
                ? args[1]
                : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "demo");

            var dataset = args.Length >= 3 ? args[2] : "DemoDataset";

            await ingestWf.RunAsync(input, dataset);
            Console.WriteLine("Ingest finished.");
            break;
        }

    case "eval":
        {
            // usage:
            //   eval <dataset>            -> load persisted embeddings from FileStore
            //   eval <dataset> --sim      -> use simulated embeddings (old behavior)
            var dataset = args.Length >= 2 ? args[1] : "DemoDataset";
            var useSim = args.Any(a => string.Equals(a, "--sim", StringComparison.OrdinalIgnoreCase));

            List<ReadOnlyMemory<float>> queries;
            List<ReadOnlyMemory<float>> refs;

            if (useSim)
            {
                // --- simulated embeddings (kept for quick smoke tests) ---
                var q1 = await provider.GetEmbeddingAsync("query one");
                var q2 = await provider.GetEmbeddingAsync("query two");
                var r1 = await provider.GetEmbeddingAsync("answer one");
                var r2 = await provider.GetEmbeddingAsync("answer two");
                queries = new() { q1, q2 };
                refs = new() { r1, r2 };
                Console.WriteLine("Eval mode: simulated embeddings (--sim).");
            }

            else
            {
                // --- load persisted embeddings for this dataset from FileStore/data ---
                var dataRoot = Path.Combine(AppContext.BaseDirectory, "data", "embeddings");
                queries = Helpers.LoadVectorsBySpace(dataRoot, dataset + ":queries");
                refs = Helpers.LoadVectorsBySpace(dataRoot, dataset + ":refs");

                if (queries.Count == 0 || refs.Count == 0)
                {
                    Console.WriteLine($"No persisted embeddings under any configured root for dataset '{dataset}'.");
                    return;
                }

                Console.WriteLine($"Eval mode: persisted embeddings (dataset '{dataset}'): {queries.Count} queries vs {refs.Count} refs.");
            }

            // No real shift yet → identity via NullShift
            IShift shift = new NullShift();

            // TODO [Baseline]: Always include NoShift (Method A) as baseline in evaluation runs.
            // This ensures all Δ-metrics (e.g., cosine delta, nDCG diff, MRR change) are computed
            // consistently against the unshifted embeddings.

            evalWf.Run(shift, queries, refs, dataset);
            break;
        }

    case "ingest-queries":
        {
            // usage: ingest-queries <path> <dataset>
            var input = args.Length >= 3
                ? args[1]
                : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "demo");

            var dataset = args.Length >= 3 ? args[2] : "DemoDataset";

            await ingestWf.RunAsync(input, dataset + ":queries");
            Console.WriteLine("Ingest (queries) finished.");
            break;
        }

    case "ingest-refs":
        {
            // usage: ingest-refs <path> <dataset>
            var input = args.Length >= 3
                ? args[1]
                : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "demo");

            var dataset = args.Length >= 3 ? args[2] : "DemoDataset";

            await ingestWf.RunAsync(input, dataset + ":refs");
            Console.WriteLine("Ingest (refs) finished.");
            break;
        }

    case "adaptive":
        {
            IShiftGenerator generator = new DeltaShiftGenerator(); // or MultiplicativeShiftGenerator
            var service = new ShiftEvaluationService(generator, EvaluatorCatalog.Defaults);

            var wf = new AdaptiveWorkflow(generator, service, method);

            Console.WriteLine($"Adaptive ready (method={method}).");
            // Beispiel-Aufruf (später):
            // var best = wf.Run(queries[0], refs);

            break;
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

            // Workflow-Instanz wie im Test
            IWorkflow workflow = new FileBasedInsuranceMiniWorkflow();
            var wfRunner = new StatsAwareWorkflowRunner();

            // Läuft wie im FileBasedInsuranceMiniWorkflowTests, nur mit anderem Namen.
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
            // Full end-to-end pipeline for Mini-Insurance (baseline, First, First+Delta, LearnedDelta).
            var pipeline = new EmbeddingShift.ConsoleEval.MiniInsurance.MiniInsuranceFirstDeltaPipeline(
                msg => Console.WriteLine(msg));

            await pipeline.RunAsync(includeLearnedDelta: true);
            break;
        }
    case "mini-insurance-training-inspect":
        // Inspect latest Mini-Insurance training result under
        // local/mini-insurance/training/history
        await MiniInsuranceTrainingInspectCommand.RunAsync(args.Skip(1).ToArray());
        break;

    case "mini-insurance-training-list":
        await MiniInsuranceTrainingListCommand.RunAsync(args.Skip(1).ToArray());
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

                Console.WriteLine("Metric                AvgBaseline  AvgFirst    AvgFirst+Delta   AvgΔFirst-BL   AvgΔFirst+Delta-BL");
                Console.WriteLine("-------------------   -----------  ---------   --------------   ------------   -------------------");

                foreach (var row in aggregate.Metrics)
                {
                    Console.WriteLine(
                        $"{row.Metric,-19}   {row.AverageBaseline,11:F3}  {row.AverageFirst,9:F3}   {row.AverageFirstPlusDelta,14:F3}   {row.AverageDeltaFirstVsBaseline,12:+0.000;-0.000;0.000}   {row.AverageDeltaFirstPlusDeltaVsBaseline,19:+0.000;-0.000;0.000}");
                }

                Console.WriteLine();
                Console.WriteLine("[MiniInsurance] Aggregate done.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MiniInsurance] ERROR: Could not aggregate comparison runs under '{baseDir}': {ex.Message}");
            }

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

            // Persist runs + comparison so sie wieder in die Aggregation einfließen können.
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
        {
            var root = DirectoryLayout.ResolveResultsRoot("insurance");
            ShiftTrainingResultInspector.PrintLatest(
                workflowName: "mini-insurance-first-delta",
                rootDirectory: root);
            break;
        }
    case "mini-insurance-shift-training-history":
        {
            var root = DirectoryLayout.ResolveResultsRoot("insurance");
            ShiftTrainingResultInspector.PrintHistory(
                workflowName: "mini-insurance-first-delta",
                rootDirectory: root,
                maxItems: 20);
            break;
        }
        case "mini-insurance-shift-training-best":
        {
            var root = DirectoryLayout.ResolveResultsRoot("insurance");
            ShiftTrainingResultInspector.PrintBest(
                workflowName: "mini-insurance-first-delta",
                rootDirectory: root);
            break;
        }
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
        Console.WriteLine("  demo --shift <Name> [--dataset X]   run tiny demo (e.g., NoShift.IngestBased)");
        Console.WriteLine("  ingest-refs <path> <dataset>        ingest reference vectors");
        Console.WriteLine("  adaptive [--baseline]               adaptive shift selection (baseline = identity)");
        Console.WriteLine("  mini-insurance                      run mini insurance workflow (baseline)");
        Console.WriteLine("  mini-insurance-first-delta          compare baseline vs First/First+Delta (mini insurance)");
        Console.WriteLine("  mini-insurance-first-delta-pipeline run full Mini-Insurance First+Delta pipeline (baseline/First/First+Delta/LearnedDelta)");
        Console.WriteLine("  mini-insurance-first-delta-aggregate aggregate metrics over all comparison runs");
        Console.WriteLine("  mini-insurance-first-delta-train    train a Delta shift candidate from aggregated metrics");
        Console.WriteLine("  mini-insurance-first-delta-inspect  inspect latest trained Delta candidate");
        Console.WriteLine("  mini-insurance-first-learned-delta  compare baseline vs First vs First+LearnedDelta");
        Console.WriteLine("  mini-insurance-shift-training-history  list recent generic shift training results for mini-insurance");
        Console.WriteLine("  mini-insurance-shift-training-best     show best generic shift training result for mini-insurance");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --project src/EmbeddingShift.ConsoleEval -- demo --shift NoShift.IngestBased");
        Console.WriteLine("  dotnet run --project src/EmbeddingShift.ConsoleEval -- adaptive");
    }

    // in static class Helpers
    public static List<ReadOnlyMemory<float>> LoadVectorsBySpace(string embeddingsDir_UNUSED, string space)
    {
        // Try multiple plausible roots (bin folder, repo root, cwd)
        var candidates = new[]
        {
        Path.Combine(AppContext.BaseDirectory, "data", "embeddings"),
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "embeddings")),
        Path.Combine(Directory.GetCurrentDirectory(), "data", "embeddings")
        };

        var result = new List<ReadOnlyMemory<float>>();
        foreach (var root in candidates)
        {
            if (!Directory.Exists(root)) continue;

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

            if (result.Count > 0)
            {
                Console.WriteLine($"Found {result.Count} persisted embeddings for '{space}' under: {root}");
                break; // stop at first root that yields results
            }
        }

        if (result.Count == 0)
            Console.WriteLine($"No persisted embeddings under any known root for '{space}'. Checked: {string.Join(" | ", candidates)}");

        return result;
    }
    // Mirror of FileStore's record shape
    public sealed record EmbeddingRec(Guid id, string space, string provider, int dimensions, float[] vector);
}


