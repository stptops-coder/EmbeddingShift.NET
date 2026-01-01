using System.Text.Json;
using System.Text;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Core.Infrastructure;

namespace EmbeddingShift.Workflows.Eval
{
    public sealed record DatasetEvalRequest(
        string Dataset,
        bool UseSim = false,
        bool UseBaseline = false,
        string QueryRole = "queries",
        string RefRole = "refs");

    public sealed record DatasetEvalResult(
        string Dataset,
        bool DidRun,
        bool UsedSim,
        int QueryCount,
        int RefCount,
        string ModeLine,
        string Notes,
        Guid? RunId = null,
        string? ResultsPath = null,
        IReadOnlyDictionary<string, double>? Metrics = null,
        string? RefsManifestPath = null,
        EmbeddingShift.Workflows.ChunkFirstIngestManifestSummary? RefsManifest = null);

    /// <summary>
    /// Dataset evaluation entry: loads embeddings (simulated or persisted) and runs eval.
    /// Adds a small run_manifest.json into the results directory for lineage/repro.
    /// </summary>
    public sealed class DatasetEvalEntry
    {
        private readonly IEmbeddingProvider _provider;
        private readonly EvaluationWorkflow _evalWorkflow;

        public DatasetEvalEntry(IEmbeddingProvider provider, EvaluationWorkflow evalWorkflow)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _evalWorkflow = evalWorkflow ?? throw new ArgumentNullException(nameof(evalWorkflow));
        }

        public async Task<DatasetEvalResult> RunAsync(
            IShift shift,
            DatasetEvalRequest request,
            CancellationToken ct = default)
        {
            if (shift is null) throw new ArgumentNullException(nameof(shift));
            if (request is null) throw new ArgumentNullException(nameof(request));

            var dataset = string.IsNullOrWhiteSpace(request.Dataset) ? "DemoDataset" : request.Dataset.Trim();

            List<ReadOnlyMemory<float>> queries;
            List<ReadOnlyMemory<float>> refs;

            if (request.UseSim)
            {
                // Minimal demo vectors via provider; we only need stable, deterministic data for smoke-tests.
                // Two queries and two refs are sufficient to verify end-to-end behavior.
                ReadOnlyMemory<float> q1 = await _provider.GetEmbeddingAsync("Query 1");
                ReadOnlyMemory<float> q2 = await _provider.GetEmbeddingAsync("Query 2");

                ReadOnlyMemory<float> r1 = await _provider.GetEmbeddingAsync("Ref 1");
                ReadOnlyMemory<float> r2 = await _provider.GetEmbeddingAsync("Ref 2");

                queries = new() { q1, q2 };
                refs = new() { r1, r2 };

                var summary = request.UseBaseline
                    ? _evalWorkflow.RunWithBaselineSummary(shift, queries, refs, dataset)
                    : _evalWorkflow.RunWithSummary(shift, queries, refs, dataset);

                await WriteEvalRunManifestAsync(
                    summary,
                    dataset,
                    shift,
                    request,
                    queryCount: queries.Count,
                    refCount: refs.Count,
                    embeddingsRoot: null,
                    refsManifestPath: null,
                    refsManifest: null,
                    ct);

                return new DatasetEvalResult(
                    Dataset: dataset,
                    DidRun: true,
                    UsedSim: true,
                    QueryCount: queries.Count,
                    RefCount: refs.Count,
                    ModeLine: request.UseBaseline
                        ? "Eval mode: simulated embeddings (--sim), baseline=identity (--baseline)."
                        : "Eval mode: simulated embeddings (--sim).",
                    Notes: "",
                    RunId: summary.RunId,
                    ResultsPath: summary.ResultsPath,
                    Metrics: summary.Metrics);
            }

            // Persisted embeddings (stable data layout)
            var embeddingsRoot = DirectoryLayout.ResolveDataRoot("embeddings");

            var queriesSpace = $"{dataset}:{request.QueryRole}".Trim();
            var refsSpace = $"{dataset}:{request.RefRole}".Trim();

            var manifestsRoot = DirectoryLayout.ResolveDataRoot("manifests");

            // Prefer authoritative ingest state stored alongside persisted embeddings.
            var refsState = EmbeddingSpaceStateStore.TryRead(embeddingsRoot, refsSpace);
            var refsManifestPath = refsState?.ChunkFirstManifestPath;

            if (string.IsNullOrWhiteSpace(refsManifestPath))
                refsManifestPath = TryResolveLatestManifestPath(manifestsRoot, refsSpace);

            var refsManifest = TryReadChunkFirstManifest(refsManifestPath);

            queries = LoadVectorsForSpace(embeddingsRoot, queriesSpace);
            refs = LoadVectorsForSpace(embeddingsRoot, refsSpace);

            if (queries.Count == 0 || refs.Count == 0)
            {
                return new DatasetEvalResult(
                    Dataset: dataset,
                    DidRun: false,
                    UsedSim: false,
                    QueryCount: queries.Count,
                    RefCount: refs.Count,
                    ModeLine: "",
                    Notes: $"No persisted embeddings under '{embeddingsRoot}' for dataset '{dataset}'.");
            }

            var runSummary = request.UseBaseline
                ? _evalWorkflow.RunWithBaselineSummary(shift, queries, refs, dataset)
                : _evalWorkflow.RunWithSummary(shift, queries, refs, dataset);

            await WriteEvalRunManifestAsync(
                runSummary,
                dataset,
                shift,
                request,
                queryCount: queries.Count,
                refCount: refs.Count,
                embeddingsRoot: embeddingsRoot,
                refsManifestPath: refsManifestPath,
                refsManifest: refsManifest,
                ct);

            return new DatasetEvalResult(
                Dataset: dataset,
                DidRun: true,
                UsedSim: false,
                QueryCount: queries.Count,
                RefCount: refs.Count,
                ModeLine: request.UseBaseline
                    ? $"Eval mode: persisted embeddings (dataset '{dataset}'): {queries.Count} queries vs {refs.Count} refs. baseline=identity (--baseline)."
                    : $"Eval mode: persisted embeddings (dataset '{dataset}'): {queries.Count} queries vs {refs.Count} refs.",
                Notes: "",
                RunId: runSummary.RunId,
                ResultsPath: runSummary.ResultsPath,
                Metrics: runSummary.Metrics,
                RefsManifestPath: refsManifestPath,
                RefsManifest: refsManifest);
        }

        private static List<ReadOnlyMemory<float>> LoadVectorsForSpace(string embeddingsRoot, string space)
        {
            var logicalSpace = string.IsNullOrWhiteSpace(space) ? "default" : space.Trim();
            var spaceDir = Path.Combine(embeddingsRoot, SpaceToPath(logicalSpace));

            if (!Directory.Exists(spaceDir))
                return new List<ReadOnlyMemory<float>>();

            var files = Directory.EnumerateFiles(spaceDir, "*.json", SearchOption.TopDirectoryOnly).ToArray();
            var list = new List<ReadOnlyMemory<float>>(files.Length);

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var rec = JsonSerializer.Deserialize<EmbeddingRec>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    if (rec?.vector is { Length: > 0 })
                        list.Add(rec.vector);
                }
                catch
                {
                    // Ignore malformed files; evaluation is best-effort.
                }
            }
            return list;
        }

        // Mirror of FileStore's record shape (must match persisted JSON)
        private sealed record EmbeddingRec(Guid id, string space, string provider, int dimensions, float[] vector);

        private sealed record DatasetEvalRunManifest(
            string Kind,
            string Dataset,
            Guid RunId,
            DateTime StartedAtUtc,
            DateTime CompletedAtUtc,
            bool UsedSim,
            bool UseBaseline,
            string Shift,
            string QueryRole,
            string RefRole,
            int QueryCount,
            int RefCount,
            string? EmbeddingsRoot,
            string? RefsManifestPath,
            EmbeddingShift.Workflows.ChunkFirstIngestManifestSummary? RefsManifest);

        private static async Task WriteEvalRunManifestAsync(
            EvaluationRunSummary summary,
            string dataset,
            IShift shift,
            DatasetEvalRequest request,
            int queryCount,
            int refCount,
            string? embeddingsRoot,
            string? refsManifestPath,
            EmbeddingShift.Workflows.ChunkFirstIngestManifestSummary? refsManifest,
            CancellationToken ct)
        {
            if (summary is null) return;
            if (string.IsNullOrWhiteSpace(summary.ResultsPath)) return;

            try
            {
                Directory.CreateDirectory(summary.ResultsPath);

                var manifest = new DatasetEvalRunManifest(
                    Kind: summary.Kind,
                    Dataset: dataset,
                    RunId: summary.RunId,
                    StartedAtUtc: summary.StartedAtUtc,
                    CompletedAtUtc: summary.CompletedAtUtc,
                    UsedSim: request.UseSim,
                    UseBaseline: request.UseBaseline,
                    Shift: shift.Name,
                    QueryRole: request.QueryRole,
                    RefRole: request.RefRole,
                    QueryCount: queryCount,
                    RefCount: refCount,
                    EmbeddingsRoot: embeddingsRoot,
                    RefsManifestPath: refsManifestPath,
                    RefsManifest: refsManifest);

                var path = Path.Combine(summary.ResultsPath, "run_manifest.json");
                var json = JsonSerializer.Serialize(
                    manifest,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });

                await File.WriteAllTextAsync(path, json, new UTF8Encoding(false), ct);
            }
            catch
            {
                // Best-effort: do not fail evaluation for manifest persistence.
            }
        }

        private static string? TryResolveLatestManifestPath(string manifestsRoot, string space)
        {
            if (string.IsNullOrWhiteSpace(manifestsRoot) || string.IsNullOrWhiteSpace(space))
                return null;

            try
            {
                var dir = Path.Combine(manifestsRoot, SpaceToPath(space.Trim()));
                if (!Directory.Exists(dir)) return null;

                var latest = Path.Combine(dir, "manifest_latest.json");
                return File.Exists(latest) ? latest : null;
            }
            catch
            {
                return null;
            }
        }

        private static EmbeddingShift.Workflows.ChunkFirstIngestManifestSummary? TryReadChunkFirstManifest(string? manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
                return null;

            try
            {
                var json = File.ReadAllText(manifestPath);
                return JsonSerializer.Deserialize<EmbeddingShift.Workflows.ChunkFirstIngestManifestSummary>(
                    json,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch
            {
                return null;
            }
        }

        private static string SpaceToPath(string space)
            => SpacePath.ToRelativePath(space);
    }
}
