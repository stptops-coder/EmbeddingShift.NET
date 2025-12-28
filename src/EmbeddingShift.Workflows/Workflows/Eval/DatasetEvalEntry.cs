using System.Text.Json;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Infrastructure;

namespace EmbeddingShift.Workflows.Eval
{
    public sealed record DatasetEvalRequest(
        string Dataset,
        bool UseSim = false,
        string QueryRole = "queries",
        string RefRole = "refs");

    public sealed record DatasetEvalResult(
        string Dataset,
        bool DidRun,
        bool UsedSim,
        int QueryCount,
        int RefCount,
        string ModeLine,
        string Notes);

    /// <summary>
    /// Canonical, domain-neutral evaluation entrypoint.
    /// Loads persisted embeddings from the stable data layout and runs EvaluationWorkflow.
    /// Intended to be called by both CLI and future UI (Program.cs should stay thin glue).
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
                // Simulated embeddings (kept for quick smoke tests)
                var q1 = await _provider.GetEmbeddingAsync("query one").ConfigureAwait(false);
                var q2 = await _provider.GetEmbeddingAsync("query two").ConfigureAwait(false);
                var r1 = await _provider.GetEmbeddingAsync("answer one").ConfigureAwait(false);
                var r2 = await _provider.GetEmbeddingAsync("answer two").ConfigureAwait(false);

                queries = new() { q1, q2 };
                refs = new() { r1, r2 };

                _evalWorkflow.Run(shift, queries, refs, dataset);

                return new DatasetEvalResult(
                    Dataset: dataset,
                    DidRun: true,
                    UsedSim: true,
                    QueryCount: queries.Count,
                    RefCount: refs.Count,
                    ModeLine: "Eval mode: simulated embeddings (--sim).",
                    Notes: "");
            }

            // Persisted embeddings (stable data layout)
            var embeddingsRoot = DirectoryLayout.ResolveDataRoot("embeddings");

            var queriesSpace = $"{dataset}:{request.QueryRole}".Trim();
            var refsSpace = $"{dataset}:{request.RefRole}".Trim();

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

            _evalWorkflow.Run(shift, queries, refs, dataset);

            return new DatasetEvalResult(
                Dataset: dataset,
                DidRun: true,
                UsedSim: false,
                QueryCount: queries.Count,
                RefCount: refs.Count,
                ModeLine: $"Eval mode: persisted embeddings (dataset '{dataset}'): {queries.Count} queries vs {refs.Count} refs.",
                Notes: "");
        }

        private static List<ReadOnlyMemory<float>> LoadVectorsForSpace(string embeddingsRoot, string space)
        {
            var logicalSpace = string.IsNullOrWhiteSpace(space) ? "default" : space.Trim();
            var spaceDir = Path.Combine(embeddingsRoot, SpaceToPath(logicalSpace));

            if (!Directory.Exists(spaceDir))
                return new List<ReadOnlyMemory<float>>();

            var files = Directory.GetFiles(spaceDir, "*.json", SearchOption.TopDirectoryOnly)
                                 .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                                 .ToArray();

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
    }
}
