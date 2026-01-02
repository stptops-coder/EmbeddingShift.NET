using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using EmbeddingShift.ConsoleEval;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Infrastructure;
using EmbeddingShift.Core.Shifts;
using EmbeddingShift.Workflows.Eval;
using EmbeddingShift.Workflows.Ingest;
using EmbeddingShift.Workflows.Run;

namespace EmbeddingShift.ConsoleEval.Commands
{
    /// <summary>
    /// Dataset-oriented CLI commands extracted from Program.cs.
    /// Intentionally minimal; focuses on canonical ingest + eval/run with an acceptance gate.
    /// </summary>
    internal static class DatasetCliCommands
    {
        // Backward-compatible alias for older Program.cs variants.
        public static Task<int> IngestAsync(
            string[] args,
            ConsoleEvalHost host)
            => IngestRefsAsync(args, host);

        public static async Task<int> IngestLegacyAsync(
            string[] args,
            ConsoleEvalHost host)
        {
            var ingestEntry = host.Services.IngestEntry;
            var textLineIngestor = host.Services.TxtLineIngestor;

            var input = args.Length >= 3 ? args[1] : ResolveSamplesDemoPath();
            var dataset = args.Length >= 3 ? args[2] : "DemoDataset";

            await ingestEntry.RunAsync(
                new DatasetIngestRequest(
                    Dataset: dataset,
                    Role: "refs",
                    InputPath: input,
                    Mode: DatasetIngestMode.Plain),
                textLineIngestor: textLineIngestor);

            Console.WriteLine("Ingest finished (legacy).");
            return 0;
        }

        public static async Task<int> EvalAsync(
            string[] args,
            ConsoleEvalHost host)
        {
            var evalEntry = host.Services.EvalEntry;
            // usage:
            //   eval <dataset>                 -> load persisted embeddings from FileStore
            //   eval <dataset> --sim           -> use simulated embeddings
            //   eval <dataset> --baseline      -> compare against identity baseline (shift vs baseline metrics)
            var dataset = args.Length >= 2 ? args[1] : "DemoDataset";
            var useSim = args.Any(a => string.Equals(a, "--sim", StringComparison.OrdinalIgnoreCase));
            var useBaseline = args.Any(a => string.Equals(a, "--baseline", StringComparison.OrdinalIgnoreCase));

            var shift = ParseShift(args);
            var gateEps = ParseGateEps(args, defaultEps: 1e-6);

            var res = await evalEntry.RunAsync(
                shift,
                new DatasetEvalRequest(dataset, UseSim: useSim, UseBaseline: useBaseline));

            if (!string.IsNullOrWhiteSpace(res.ModeLine))
                Console.WriteLine(res.ModeLine);

            if (!res.DidRun)
            {
                if (!string.IsNullOrWhiteSpace(res.Notes))
                    Console.WriteLine(res.Notes);
                return 0;
            }

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

                // Best-effort: write a machine-readable gate decision next to the results directory.
                await EvalAcceptanceGateManifest.TryWriteAsync(res, gateProfile, gateRes);

                if (!gateRes.Passed)
                {
                    Environment.ExitCode = 2;
                    return 2;
                }
            }

            return 0;
        }

        public static async Task<int> RunAsync(
            string[] args,
            ConsoleEvalHost host)
        {
            var runEntry = host.Services.RunEntry;
            var textLineIngestor = host.Services.TxtLineIngestor;
            var queriesJsonIngestor = host.Services.QueriesJsonIngestor;

            // usage:
            //   run <refsPath> <queriesPath> <dataset> [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive] [--sim] [--baseline]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: run <refsPath> <queriesPath> <dataset> [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive] [--sim] [--baseline] [--shift=identity|zero] [--gate-profile=rank|rank+cosine] [--gate-eps=1e-6]");
                Environment.ExitCode = 1;
                return 1;
            }

            var refsPath = args[1];
            var queriesPath = args[2];
            var dataset = args[3];

            var refsMode = args.Any(a => string.Equals(a, "--refs-plain", StringComparison.OrdinalIgnoreCase))
                ? DatasetIngestMode.Plain
                : DatasetIngestMode.ChunkFirst;

            var chunkSize = 1000;
            var chunkOverlap = 100;
            var recursive = !args.Any(a => a.Equals("--no-recursive", StringComparison.OrdinalIgnoreCase));
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
            }

            var shift = ParseShift(args);

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
                textLineIngestor,
                queriesJsonIngestor);

            if (res.RefsIngest.Mode == DatasetIngestMode.ChunkFirst && !string.IsNullOrWhiteSpace(res.RefsIngest.ManifestPath))
                Console.WriteLine($"Refs manifest: {res.RefsIngest.ManifestPath}");

            if (!string.IsNullOrWhiteSpace(res.EvalResult.ModeLine))
                Console.WriteLine(res.EvalResult.ModeLine);

            if (!res.EvalResult.DidRun && !string.IsNullOrWhiteSpace(res.EvalResult.Notes))
                Console.WriteLine(res.EvalResult.Notes);

            if (useBaseline && res.EvalResult.DidRun)
            {
                var gateEps = ParseGateEps(args, defaultEps: 1e-6);
                var gateProfile = args.FirstOrDefault(a => a.StartsWith("--gate-profile=", StringComparison.OrdinalIgnoreCase))
                    ?.Substring("--gate-profile=".Length)
                    ?.Trim();

                var gate = EvalAcceptanceGate.CreateFromProfile(gateProfile, gateEps);
                var gateRes = gate.Evaluate(res.EvalResult.Metrics);

                Console.WriteLine($"Acceptance gate: {(gateRes.Passed ? "PASS" : "FAIL")} (eps={gateRes.Epsilon:G}).");
                foreach (var note in gateRes.Notes)
                    Console.WriteLine(note);

                // Best-effort: write a machine-readable gate decision next to the results directory.
                await EvalAcceptanceGateManifest.TryWriteAsync(res.EvalResult, gateProfile, gateRes);

                if (!gateRes.Passed)
                {
                    Environment.ExitCode = 2;
                    return 2;
                }
            }

            return 0;
        }

        public static async Task<int> RunDemoAsync(
             string[] args,
             ConsoleEvalHost host)
        {
            var runEntry = host.Services.RunEntry;
            var textLineIngestor = host.Services.TxtLineIngestor;
            var queriesJsonIngestor = host.Services.QueriesJsonIngestor;

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

            var repoRoot = ResolveRepoRoot();
            var refsPath = Path.Combine(repoRoot, "samples", "insurance", "policies");
            var queriesPath = Path.Combine(repoRoot, "samples", "insurance", "queries");

            var shift = ParseShift(args);

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
                textLineIngestor,
                queriesJsonIngestor);

            if (res.RefsIngest.Mode == DatasetIngestMode.ChunkFirst && !string.IsNullOrWhiteSpace(res.RefsIngest.ManifestPath))
                Console.WriteLine($"Refs manifest: {res.RefsIngest.ManifestPath}");

            if (!string.IsNullOrWhiteSpace(res.EvalResult.ModeLine))
                Console.WriteLine(res.EvalResult.ModeLine);

            if (!res.EvalResult.DidRun && !string.IsNullOrWhiteSpace(res.EvalResult.Notes))
                Console.WriteLine(res.EvalResult.Notes);

            if (useBaseline && res.EvalResult.DidRun)
            {
                var gateEps = ParseGateEps(args, defaultEps: 1e-6);
                var gateProfile = args.FirstOrDefault(a => a.StartsWith("--gate-profile=", StringComparison.OrdinalIgnoreCase))
                    ?.Substring("--gate-profile=".Length)
                    ?.Trim();

                var gate = EvalAcceptanceGate.CreateFromProfile(gateProfile, gateEps);
                var gateRes = gate.Evaluate(res.EvalResult.Metrics);

                Console.WriteLine($"Acceptance gate: {(gateRes.Passed ? "PASS" : "FAIL")} (eps={gateRes.Epsilon:G}).");
                foreach (var note in gateRes.Notes)
                    Console.WriteLine(note);

                // Best-effort: write a machine-readable gate decision next to the results directory.
                await EvalAcceptanceGateManifest.TryWriteAsync(res.EvalResult, gateProfile, gateRes);

                if (!gateRes.Passed)
                {
                    Environment.ExitCode = 2;
                    return 2;
                }
            }

            return 0;
        }

        public static async Task<int> RunSmokeAsync(string[] args, ConsoleEvalHost host)
        {
            // usage:
            //   run-smoke <refsPath> <queriesPath> <dataset>
            //            [--force-reset]
            //            [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive]
            //            [--sim] [--baseline]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: run-smoke <refsPath> <queriesPath> <dataset> [--force-reset] [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive] [--sim] [--baseline]");
                Environment.ExitCode = 1;
                return 1;
            }

            var refsPath = args[1];
            var queriesPath = args[2];
            var dataset = args[3];

            var forceReset = args.Any(a => a.Equals("--force-reset", StringComparison.OrdinalIgnoreCase));

            Console.WriteLine($"[SMOKE] dataset={dataset}");
            Console.WriteLine($"  refs   = {refsPath}");
            Console.WriteLine($"  queries= {queriesPath}");
            Console.WriteLine($"  reset  = {forceReset}");
            Console.WriteLine();

            if (forceReset)
            {
                var rcReset = await DatasetResetAsync(new[] { "dataset-reset", dataset, "--role=all", "--force" });
                if (rcReset != 0)
                    return rcReset;
            }

            // Pass-through only the ingest-related flags we support here (avoid accidental unknowns).
            static bool IsIngestFlag(string a) =>
                a.Equals("--refs-plain", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--no-recursive", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--sim", StringComparison.OrdinalIgnoreCase) ||
                a.StartsWith("--chunk-size=", StringComparison.OrdinalIgnoreCase) ||
                a.StartsWith("--chunk-overlap=", StringComparison.OrdinalIgnoreCase);

            var ingestArgs = new[] { "ingest-dataset", refsPath, queriesPath, dataset }
                .Concat(args.Skip(4).Where(IsIngestFlag))
                .ToArray();

            var rcIngest = await IngestDatasetAsync(ingestArgs, host);
            if (rcIngest != 0)
                return rcIngest;

            // Validate:
            // - refs: require state + chunk manifest
            // - queries: require state only (queries typically have no chunk manifest)
            var rcV1 = await DatasetValidateAsync(new[] { "dataset-validate", dataset, "--role=refs", "--require-state", "--require-chunk-manifest" });
            if (rcV1 != 0)
                return rcV1;

            var rcV2 = await DatasetValidateAsync(new[] { "dataset-validate", dataset, "--role=queries", "--require-state" });
            if (rcV2 != 0)
                return rcV2;

            static bool IsEvalFlag(string a) =>
                a.Equals("--sim", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--baseline", StringComparison.OrdinalIgnoreCase);

            var evalArgs = new[] { "eval", dataset }
                .Concat(args.Skip(4).Where(IsEvalFlag))
                .ToArray();

            var rcEval = await EvalAsync(evalArgs, host);
            if (rcEval != 0)
                return rcEval;

            Console.WriteLine();
            Console.WriteLine("[SMOKE] PASS");
            return 0;
        }

        public static Task<int> RunSmokeDemoAsync(string[] args, ConsoleEvalHost host)
        {
            // usage:
            //   run-smoke-demo <datasetName> [--force-reset] [--baseline] [--sim] [--refs-plain] [--chunk-size=N] [--chunk-overlap=N]
            var dataset = args.Length > 1 && !args[1].StartsWith("-", StringComparison.Ordinal)
                ? args[1]
                : "DemoDataset";

            // Reuse same demo asset resolution as run-demo uses (canonical place).
            // If your solution already has another resolver, keep this minimal and local.
            DemoAssets demo;
            try
            {
                demo = DemoDatasetAssets.Resolve();
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine(ex.Message);
                Environment.ExitCode = 2;
                return Task.FromResult(2);
            }
                      
            var passThrough = args.Skip(1)
                .Where(a => a.StartsWith("-", StringComparison.Ordinal))
                .ToArray();

            var smokeArgs = new[] { "run-smoke", demo.RefsPath, demo.QueriesPath, dataset }
                .Concat(passThrough)
                .ToArray();

            return RunSmokeAsync(smokeArgs, host);
        }

        private sealed record DemoAssets(string RefsPath, string QueriesPath);

        private static class DemoDatasetAssets
        {
            public static DemoAssets Resolve()
            {
                // Align with run-demo: samples/insurance/policies + samples/insurance/queries
                var repoRoot = ResolveRepoRoot();

                var refsDir = Path.Combine(repoRoot, "samples", "insurance", "policies");
                var queriesDir = Path.Combine(repoRoot, "samples", "insurance", "queries");

                if (!Directory.Exists(refsDir))
                    throw new FileNotFoundException($"Demo refs directory not found: {refsDir}");

                if (!Directory.Exists(queriesDir))
                    throw new FileNotFoundException($"Demo queries directory not found: {queriesDir}");

                return new DemoAssets(refsDir, queriesDir);
            }
        }

        public static async Task<int> IngestQueriesAsync(
             string[] args,
             ConsoleEvalHost host)
        {
            var ingestEntry = host.Services.IngestEntry;
            var textLineIngestor = host.Services.TxtLineIngestor;
            var queriesJsonIngestor = host.Services.QueriesJsonIngestor;

            var input = args.Length >= 3 ? args[1] : ResolveSamplesDemoPath();
            var dataset = args.Length >= 3 ? args[2] : "DemoDataset";

            await ingestEntry.RunAsync(
                new DatasetIngestRequest(
                    Dataset: dataset,
                    Role: "queries",
                    InputPath: input,
                    Mode: DatasetIngestMode.Plain),
                textLineIngestor: textLineIngestor,
                queriesJsonIngestor: queriesJsonIngestor);

            Console.WriteLine("Ingest (queries) finished.");
            return 0;
        }

        public static async Task<int> IngestDatasetAsync(
            string[] args,
            ConsoleEvalHost host)
        {
            var ingestDatasetEntry = host.Services.IngestDatasetEntry;
            var textLineIngestor = host.Services.TxtLineIngestor;
            var queriesJsonIngestor = host.Services.QueriesJsonIngestor;

            // usage:
            //   ingest-dataset <refsPath> <queriesPath> <dataset> [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive]
            if (args.Length < 4)
            {
                Console.WriteLine("Usage: ingest-dataset <refsPath> <queriesPath> <dataset> [--refs-plain] [--chunk-size=N] [--chunk-overlap=N] [--no-recursive]");
                Environment.ExitCode = 1;
                return 1;
            }

            var refsPath = args[1];
            var queriesPath = args[2];

            // Allow passing a directory for queries (e.g. samples/insurance/queries)
            // and auto-resolve a concrete query file (prefer queries.json).
            if (Directory.Exists(queriesPath))
            {
                var preferred = Path.Combine(queriesPath, "queries.json");
                if (File.Exists(preferred))
                {
                    queriesPath = preferred;
                }
                else
                {
                    var firstJson = Directory.EnumerateFiles(queriesPath, "*.json", SearchOption.TopDirectoryOnly)
                        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault();

                    if (firstJson is not null)
                        queriesPath = firstJson;
                }
            }

            var dataset = args[3];

            if (!File.Exists(queriesPath))
            {
                Console.WriteLine($"ERROR: queries file not found: {queriesPath}");
                Console.WriteLine("Hint: use an absolute path or create q.txt in the current directory.");
                Environment.ExitCode = 1;
                return 1;
            }

            if (!(File.Exists(refsPath) || Directory.Exists(refsPath)))
            {
                Console.WriteLine($"ERROR: refs path not found: {refsPath}");
                Console.WriteLine("Hint: use an absolute path or create r.txt in the current directory.");
                Environment.ExitCode = 1;
                return 1;
            }

            var refsMode = args.Any(a => string.Equals(a, "--refs-plain", StringComparison.OrdinalIgnoreCase))
                ? DatasetIngestMode.Plain
                : DatasetIngestMode.ChunkFirst;

            var chunkSize = 1000;
            var chunkOverlap = 100;
            var recursive = !args.Any(a => a.Equals("--no-recursive", StringComparison.OrdinalIgnoreCase));

            foreach (var a in args)
            {
                if (a.StartsWith("--chunk-size=", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(a.Substring("--chunk-size=".Length), out var cs) && cs > 0)
                    chunkSize = cs;

                if (a.StartsWith("--chunk-overlap=", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(a.Substring("--chunk-overlap=".Length), out var co) && co >= 0)
                    chunkOverlap = co;
            }

            var res = await ingestDatasetEntry.RunAsync(
                new DatasetIngestDatasetRequest(
                    Dataset: dataset,
                    RefsPath: refsPath,
                    QueriesPath: queriesPath,
                    RefsMode: refsMode,
                    ChunkSize: chunkSize,
                    ChunkOverlap: chunkOverlap,
                    Recursive: recursive),
                textLineIngestor,
                queriesJsonIngestor);

            if (res.RefsIngest.Mode == DatasetIngestMode.ChunkFirst && !string.IsNullOrWhiteSpace(res.RefsIngest.ManifestPath))
                Console.WriteLine($"Refs manifest: {res.RefsIngest.ManifestPath}");

            if (res.QueriesIngest.UsedJson)
                Console.WriteLine("Queries ingested from queries.json.");

            Console.WriteLine("Ingest (dataset) finished.");
            return 0;
        }

        public static async Task<int> IngestRefsAsync(
            string[] args,
            ConsoleEvalHost host)
        {
            var ingestEntry = host.Services.IngestEntry;
            var textLineIngestor = host.Services.TxtLineIngestor;

            var input = args.Length >= 3 ? args[1] : ResolveSamplesDemoPath();
            var dataset = args.Length >= 3 ? args[2] : "DemoDataset";

            await ingestEntry.RunAsync(
                new DatasetIngestRequest(
                    Dataset: dataset,
                    Role: "refs",
                    InputPath: input,
                    Mode: DatasetIngestMode.Plain),
                textLineIngestor: textLineIngestor);

            Console.WriteLine("Ingest (refs) finished.");
            return 0;
        }

        public static async Task<int> IngestRefsChunkedAsync(
            string[] args,
            ConsoleEvalHost host)
        {
            var ingestEntry = host.Services.IngestEntry;
            var textLineIngestor = host.Services.TxtLineIngestor;

            // usage: ingest-refs-chunked <path> <dataset> [--chunk-size=1000] [--chunk-overlap=100] [--no-recursive]
            var input = args.Length >= 3 ? args[1] : ResolveSamplesDemoPath();
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
                textLineIngestor: textLineIngestor);

            Console.WriteLine($"Ingest (refs, chunked) finished. Manifest: {result.ManifestPath}");
            return 0;
        }

        public static Task<int> IngestInspectAsync(string[] args)
        {
            // usage:
            //   ingest-inspect <dataset> [--role=refs|queries]
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: ingest-inspect <dataset> [--role=refs|queries]");
                Environment.ExitCode = 1;
                return Task.FromResult(1);
            }

            var dataset = args[1].Trim();
            var role = args.FirstOrDefault(a => a.StartsWith("--role=", StringComparison.OrdinalIgnoreCase))
                         ?.Split('=', 2)[1]
                         ?.Trim();

            if (string.IsNullOrWhiteSpace(role))
                role = "refs";

            var space = $"{dataset}:{role}".Trim();
            var embeddingsRoot = DirectoryLayout.ResolveDataRoot("embeddings");

            var state = EmbeddingSpaceStateStore.TryRead(embeddingsRoot, space);
            if (state is null)
            {
                Console.WriteLine($"No ingest state found for space '{space}'.");
                Environment.ExitCode = 1;
                return Task.FromResult(1);
            }

            Console.WriteLine($"[INGEST STATE] space={state.Space}");
            Console.WriteLine($"  mode      = {state.Mode}");
            Console.WriteLine($"  provider  = {state.Provider}");
            Console.WriteLine($"  usedJson  = {state.UsedJson}");
            Console.WriteLine($"  createdUtc= {state.CreatedUtc:O}");

            if (!string.IsNullOrWhiteSpace(state.ChunkFirstManifestPath))
            {
                Console.WriteLine($"  manifest  = {state.ChunkFirstManifestPath}");

                try
                {
                    if (File.Exists(state.ChunkFirstManifestPath))
                    {
                        var json = File.ReadAllText(state.ChunkFirstManifestPath);
                        var summary = JsonSerializer.Deserialize<EmbeddingShift.Workflows.ChunkFirstIngestManifestSummary>(
                            json,
                            new JsonSerializerOptions(JsonSerializerDefaults.Web));

                        if (summary is not null)
                        {
                            Console.WriteLine("  [MANIFEST SUMMARY]");
                            Console.WriteLine($"    id            = {summary.Id}");
                            Console.WriteLine($"    inputRoot     = {summary.InputRoot}");
                            Console.WriteLine($"    totalDocs     = {summary.TotalDocuments}");
                            Console.WriteLine($"    totalChunks   = {summary.TotalChunks}");
                            Console.WriteLine($"    dims          = {summary.Dimensions}");
                            Console.WriteLine($"    chunkSize     = {summary.Preprocessing.ChunkSize}");
                            Console.WriteLine($"    chunkOverlap  = {summary.Preprocessing.ChunkOverlap}");
                            Console.WriteLine($"    chunkIndex    = {summary.ChunkIndexFileName}");
                        }
                    }
                }
                catch
                {
                    // keep inspect resilient
                }
            }

            return Task.FromResult(0);
        }

        public static Task<int> DatasetStatusAsync(string[] args)
        {
            // usage:
            //   dataset-status <dataset> [--role=refs|queries|all]
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: dataset-status <dataset> [--role=refs|queries|all]");
                Environment.ExitCode = 1;
                return Task.FromResult(1);
            }

            var dataset = args[1].Trim();

            var role = args.FirstOrDefault(a => a.StartsWith("--role=", StringComparison.OrdinalIgnoreCase))
                ?.Split('=', 2)[1]
                ?.Trim()
                ?.ToLowerInvariant();

            var roles = role switch
            {
                "refs" => new[] { "refs" },
                "queries" => new[] { "queries" },
                _ => new[] { "refs", "queries" }
            };

            var embeddingsRoot = DirectoryLayout.ResolveDataRoot("embeddings");
            var manifestsRoot = DirectoryLayout.ResolveDataRoot("manifests");

            Console.WriteLine($"[DATASET STATUS] dataset={dataset}");
            Console.WriteLine($"  embeddingsRoot = {embeddingsRoot}");
            Console.WriteLine($"  manifestsRoot  = {manifestsRoot}");

            foreach (var r in roles)
            {
                var space = $"{dataset}:{r}";
                var rel = SpacePath.ToRelativePath(space);

                var spaceDir = Path.Combine(embeddingsRoot, rel);
                var statePath = EmbeddingSpaceStateStore.ResolveStatePath(embeddingsRoot, space);
                var latestManifest = Path.Combine(manifestsRoot, rel, "manifest_latest.json");

                Console.WriteLine();
                Console.WriteLine($"  [SPACE] {space}");
                Console.WriteLine($"    dir         = {spaceDir}");
                Console.WriteLine($"    dirExists   = {Directory.Exists(spaceDir)}");
                Console.WriteLine($"    state       = {statePath}");
                Console.WriteLine($"    stateExists = {File.Exists(statePath)}");
                Console.WriteLine($"    latestManif = {latestManifest}");
                Console.WriteLine($"    latestExist = {File.Exists(latestManifest)}");

                var state = EmbeddingSpaceStateStore.TryRead(embeddingsRoot, space);
                if (state is null)
                {
                    Console.WriteLine("    stateRead   = <none>");
                    continue;
                }

                Console.WriteLine($"    mode        = {state.Mode}");
                Console.WriteLine($"    provider    = {state.Provider}");
                Console.WriteLine($"    usedJson    = {state.UsedJson}");
                Console.WriteLine($"    createdUtc  = {state.CreatedUtc:O}");

                if (!string.IsNullOrWhiteSpace(state.ChunkFirstManifestPath))
                {
                    Console.WriteLine($"    manifest    = {state.ChunkFirstManifestPath}");
                    Console.WriteLine($"    manifestOk  = {File.Exists(state.ChunkFirstManifestPath)}");
                    TryPrintManifestSummary(state.ChunkFirstManifestPath);
                }
            }

            return Task.FromResult(0);
        }
        public static Task<int> DatasetResetAsync(string[] args)
        {
            // usage:
            //   dataset-reset <dataset> [--role=refs|queries|all] [--force] [--keep-manifests]
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: dataset-reset <dataset> [--role=refs|queries|all] [--force] [--keep-manifests]");
                Environment.ExitCode = 1;
                return Task.FromResult(1);
            }

            var dataset = args[1].Trim();

            var role = args.FirstOrDefault(a => a.StartsWith("--role=", StringComparison.OrdinalIgnoreCase))
                ?.Split('=', 2)[1]
                ?.Trim()
                ?.ToLowerInvariant();

            var force = args.Any(a => a.Equals("--force", StringComparison.OrdinalIgnoreCase));
            var keepManifests = args.Any(a => a.Equals("--keep-manifests", StringComparison.OrdinalIgnoreCase));

            var roles = role switch
            {
                "refs" => new[] { "refs" },
                "queries" => new[] { "queries" },
                _ => new[] { "refs", "queries" }
            };

            var embeddingsRoot = DirectoryLayout.ResolveDataRoot("embeddings");
            var manifestsRoot = DirectoryLayout.ResolveDataRoot("manifests");

            Console.WriteLine($"[DATASET RESET] dataset={dataset}");
            Console.WriteLine($"  embeddingsRoot = {embeddingsRoot}");
            Console.WriteLine($"  manifestsRoot  = {manifestsRoot}");
            Console.WriteLine($"  roles          = {string.Join(", ", roles)}");
            Console.WriteLine($"  keepManifests  = {keepManifests}");
            Console.WriteLine($"  mode           = {(force ? "DELETE" : "PREVIEW (use --force)")}");
            Console.WriteLine();

            var hadError = false;

            foreach (var r in roles)
            {
                var space = $"{dataset}:{r}";
                var rel = SpacePath.ToRelativePath(space);

                var embeddingsDir = Path.Combine(embeddingsRoot, rel);
                var statePath = EmbeddingSpaceStateStore.ResolveStatePath(embeddingsRoot, space);
                var manifestsDir = Path.Combine(manifestsRoot, rel);

                Console.WriteLine($"  [SPACE] {space}");
                Console.WriteLine($"    embeddingsDir = {embeddingsDir}");
                Console.WriteLine($"    statePath     = {statePath}");
                Console.WriteLine($"    manifestsDir  = {manifestsDir}");

                if (!force)
                {
                    Console.WriteLine("    action        = (preview only)");
                    Console.WriteLine();
                    continue;
                }

                try
                {
                    if (Directory.Exists(embeddingsDir))
                    {
                        Directory.Delete(embeddingsDir, recursive: true);
                        Console.WriteLine("    deleted       = embeddingsDir");
                    }

                    if (File.Exists(statePath))
                    {
                        File.Delete(statePath);
                        Console.WriteLine("    deleted       = statePath");
                    }

                    if (!keepManifests && Directory.Exists(manifestsDir))
                    {
                        Directory.Delete(manifestsDir, recursive: true);
                        Console.WriteLine("    deleted       = manifestsDir");
                    }
                }
                catch (Exception ex)
                {
                    hadError = true;
                    Console.WriteLine($"    ERROR         = {ex.GetType().Name}: {ex.Message}");
                }

                Console.WriteLine();
            }

            if (hadError)
            {
                Environment.ExitCode = 2;
                return Task.FromResult(2);
            }

            return Task.FromResult(0);
        }

        private static void TryPrintManifestSummary(string manifestPath)
        {
            try
            {
                if (!File.Exists(manifestPath))
                    return;

                var json = File.ReadAllText(manifestPath);

                var summary = JsonSerializer.Deserialize<EmbeddingShift.Workflows.ChunkFirstIngestManifestSummary>(
                    json,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));

                if (summary is null)
                    return;

                Console.WriteLine("    [MANIFEST SUMMARY]");
                Console.WriteLine($"      totalDocs   = {summary.TotalDocuments}");
                Console.WriteLine($"      totalChunks = {summary.TotalChunks}");
                Console.WriteLine($"      dims        = {summary.Dimensions}");
                Console.WriteLine($"      chunkSize   = {summary.Preprocessing.ChunkSize}");
                Console.WriteLine($"      overlap     = {summary.Preprocessing.ChunkOverlap}");
            }
            catch
            {
                // best-effort only
            }
        }
        public static Task<int> DatasetValidateAsync(string[] args)
        {
            // usage:
            //   dataset-validate <dataset> [--role=refs|queries|all] [--require-state] [--require-chunk-manifest]
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: dataset-validate <dataset> [--role=refs|queries|all] [--require-state] [--require-chunk-manifest]");
                Environment.ExitCode = 1;
                return Task.FromResult(1);
            }

            var dataset = args[1].Trim();

            var role = args.FirstOrDefault(a => a.StartsWith("--role=", StringComparison.OrdinalIgnoreCase))
                ?.Split('=', 2)[1]
                ?.Trim()
                ?.ToLowerInvariant();

            var requireState = args.Any(a => a.Equals("--require-state", StringComparison.OrdinalIgnoreCase));
            var requireChunkManifest = args.Any(a => a.Equals("--require-chunk-manifest", StringComparison.OrdinalIgnoreCase));

            var roles = role switch
            {
                "refs" => new[] { "refs" },
                "queries" => new[] { "queries" },
                _ => new[] { "refs", "queries" }
            };

            var embeddingsRoot = DirectoryLayout.ResolveDataRoot("embeddings");
            var manifestsRoot = DirectoryLayout.ResolveDataRoot("manifests");

            Console.WriteLine($"[DATASET VALIDATE] dataset={dataset}");
            Console.WriteLine($"  embeddingsRoot = {embeddingsRoot}");
            Console.WriteLine($"  manifestsRoot  = {manifestsRoot}");
            Console.WriteLine($"  roles          = {string.Join(", ", roles)}");
            Console.WriteLine($"  requireState   = {requireState}");
            Console.WriteLine($"  requireChunkMf = {requireChunkManifest}");
            Console.WriteLine();

            var ok = true;

            foreach (var r in roles)
            {
                var space = $"{dataset}:{r}";
                var rel = SpacePath.ToRelativePath(space);

                var embeddingsDir = Path.Combine(embeddingsRoot, rel);
                var statePath = EmbeddingSpaceStateStore.ResolveStatePath(embeddingsRoot, space);
                var latestManifest = Path.Combine(manifestsRoot, rel, "manifest_latest.json");

                Console.WriteLine($"  [SPACE] {space}");
                Console.WriteLine($"    embeddingsDir = {embeddingsDir}");
                Console.WriteLine($"    statePath     = {statePath}");
                Console.WriteLine($"    latestManif   = {latestManifest}");

                var dirExists = Directory.Exists(embeddingsDir);
                Console.WriteLine($"    dirExists     = {dirExists}");
                if (!dirExists) ok = false;

                var embCount = 0;
                try
                {
                    if (dirExists)
                        embCount = Directory.GetFiles(embeddingsDir, "*.json", SearchOption.TopDirectoryOnly).Length;
                }
                catch
                {
                    // ignore, will fail below via count=0
                }

                Console.WriteLine($"    embFiles      = {embCount}");
                if (embCount <= 0) ok = false;

                var stateExists = File.Exists(statePath);
                Console.WriteLine($"    stateExists   = {stateExists}");

                if (requireState && !stateExists)
                    ok = false;

                var state = EmbeddingSpaceStateStore.TryRead(embeddingsRoot, space);
                if (state is null)
                {
                    Console.WriteLine("    stateRead     = <none>");
                    if (requireState) ok = false;
                }
                else
                {
                    Console.WriteLine($"    provider      = {state.Provider}");
                    Console.WriteLine($"    mode          = {state.Mode}");
                    Console.WriteLine($"    usedJson      = {state.UsedJson}");
                    Console.WriteLine($"    createdUtc    = {state.CreatedUtc:O}");

                    if (!string.IsNullOrWhiteSpace(state.ChunkFirstManifestPath))
                    {
                        var mfOk = File.Exists(state.ChunkFirstManifestPath);
                        Console.WriteLine($"    chunkManifest = {state.ChunkFirstManifestPath}");
                        Console.WriteLine($"    mfExists      = {mfOk}");
                        if (!mfOk) ok = false;
                    }
                    else
                    {
                        Console.WriteLine("    chunkManifest = <none>");
                        // typically OK for queries; only force if requested
                        if (requireChunkManifest) ok = false;
                    }
                }

                Console.WriteLine($"    latestExists  = {File.Exists(latestManifest)}");
                Console.WriteLine();
            }

            if (!ok)
            {
                Console.WriteLine("Validation: FAIL");
                Environment.ExitCode = 2;
                return Task.FromResult(2);
            }

            Console.WriteLine("Validation: PASS");
            return Task.FromResult(0);
        }

        private static IShift ParseShift(string[] args)
        {
            var shiftArg = args.FirstOrDefault(a => a.StartsWith("--shift=", StringComparison.OrdinalIgnoreCase))
                ?.Substring("--shift=".Length)
                ?.Trim();

            return (shiftArg ?? "identity").ToLowerInvariant() switch
            {
                "zero" => new MultiplicativeShift(0f, EmbeddingDimensions.DIM),
                _ => new NoShiftIngestBased(),
            };
        }

        private static double ParseGateEps(string[] args, double defaultEps)
        {
            var gateEpsArg = args.FirstOrDefault(a => a.StartsWith("--gate-eps=", StringComparison.OrdinalIgnoreCase))
                ?.Substring("--gate-eps=".Length)
                ?.Trim();

            if (!string.IsNullOrWhiteSpace(gateEpsArg) &&
                double.TryParse(gateEpsArg, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return defaultEps;
        }

        private static string ResolveSamplesDemoPath()
        {
            var repoRoot = ResolveRepoRoot();
            return Path.Combine(repoRoot, "samples", "demo");
        }

        private static string ResolveRepoRoot()
        {
            var dataRoot = DirectoryLayout.ResolveDataRoot();
            var repoRoot = Path.GetFullPath(Path.Combine(dataRoot, ".."));

            if (RepositoryLayout.TryResolveRepoRoot(out var rr))
                repoRoot = rr;

            return repoRoot;
        }
    }
}
