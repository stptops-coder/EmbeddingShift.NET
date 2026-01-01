using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            DatasetIngestEntry ingestEntry,
            IIngestor textLineIngestor)
            => IngestRefsAsync(args, ingestEntry, textLineIngestor);

        public static async Task<int> IngestLegacyAsync(
            string[] args,
            DatasetIngestEntry ingestEntry,
            IIngestor textLineIngestor)
        {
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
            DatasetEvalEntry evalEntry)
        {
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
            DatasetRunEntry runEntry,
            IIngestor textLineIngestor,
            IIngestor queriesJsonIngestor)
        {
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
            DatasetRunEntry runEntry,
            IIngestor textLineIngestor,
            IIngestor queriesJsonIngestor)
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

        public static async Task<int> IngestQueriesAsync(
            string[] args,
            DatasetIngestEntry ingestEntry,
            IIngestor textLineIngestor,
            IIngestor queriesJsonIngestor)
        {
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
            DatasetIngestDatasetEntry ingestDatasetEntry,
            IIngestor textLineIngestor,
            IIngestor queriesJsonIngestor)
        {
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
            var dataset = args[3];

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
            DatasetIngestEntry ingestEntry,
            IIngestor textLineIngestor)
        {
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
            DatasetIngestEntry ingestEntry,
            IIngestor textLineIngestor)
        {
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
