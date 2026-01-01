using System.Text.Json;
using EmbeddingShift.Core.Infrastructure;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Persistence
{
    /// <summary>
    /// Simple JSONL/JSON file-based vector store.
    /// - Embeddings are saved under: data/embeddings/{space}/{id}.json
    /// - Shifts under:               data/shifts/{id}.json
    /// - Runs under:                 data/runs/{runId}.json
    /// </summary>
    public sealed class FileStore : IVectorStore
    {
        private readonly string _root;
        private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        };

        public FileStore(string root)
        {
            _root = root;
            Directory.CreateDirectory(Path.Combine(_root, "embeddings"));
            Directory.CreateDirectory(Path.Combine(_root, "shifts"));
            Directory.CreateDirectory(Path.Combine(_root, "runs"));
        }

        public async Task SaveEmbeddingAsync(Guid id, float[] vector, string space, string provider, int dimensions)
        {
            // Keep the logical space as-is (e.g., "DemoDataset:queries") in JSON,
            // but map it to a filesystem-safe subpath for directories.
            var logicalSpace = string.IsNullOrWhiteSpace(space) ? "default" : space.Trim();
            var spaceSubPath = SpaceToPath(logicalSpace); // e.g., "DemoDataset\queries"

            var spaceDir = Path.Combine(_root, "embeddings", spaceSubPath);
            Directory.CreateDirectory(spaceDir);

            var rec = new EmbeddingRec(id, logicalSpace, provider, dimensions, vector);
            var path = Path.Combine(spaceDir, $"{id:N}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(rec, J));
        }

        // --- add these helpers inside the FileStore class ---
        private static string SpaceToPath(string space)
            => SpacePath.ToRelativePath(space);

        public async Task<float[]> LoadEmbeddingAsync(Guid id)
        {
            // search in all space folders
            var rootDir = Path.Combine(_root, "embeddings");
            if (!Directory.Exists(rootDir)) return Array.Empty<float>();

            foreach (var file in Directory.EnumerateFiles(rootDir, $"{id:N}.json", SearchOption.AllDirectories))
            {
                var json = await File.ReadAllTextAsync(file);
                var rec = JsonSerializer.Deserialize<EmbeddingRec>(json, J);
                if (rec?.vector is { Length: > 0 })
                    return rec.vector;
            }
            return Array.Empty<float>();
        }

        public async Task SaveShiftAsync(Guid id, string type, string parametersJson)
        {
            var rec = new ShiftRec(id, type, parametersJson, DateTime.UtcNow);
            var path = Path.Combine(_root, "shifts", $"{id:N}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(rec, J));
        }

        public Task<IEnumerable<(Guid id, string type, string parametersJson)>> LoadShiftsAsync()
        {
            var dir = Path.Combine(_root, "shifts");
            if (!Directory.Exists(dir))
                return Task.FromResult<IEnumerable<(Guid, string, string)>>(Array.Empty<(Guid, string, string)>());

            var items = Directory.EnumerateFiles(dir, "*.json")
                .Select(f =>
                {
                    var o = JsonSerializer.Deserialize<ShiftRec>(File.ReadAllText(f), J);
                    return (o!.id, o.type, o.parameters);
                });

            return Task.FromResult(items);
        }

        public async Task SaveRunAsync(Guid runId, string kind, string dataset, DateTime startedAt, DateTime completedAt, string resultsPath)
        {
            var rec = new RunRec(runId, kind, dataset, startedAt, completedAt, resultsPath);
            var path = Path.Combine(_root, "runs", $"{runId:N}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(rec, J));
        }

        private sealed record EmbeddingRec(Guid id, string space, string provider, int dimensions, float[] vector);
        private sealed record ShiftRec(Guid id, string type, string parameters, DateTime savedAt);
        private sealed record RunRec(Guid runId, string kind, string dataset, DateTime startedAt, DateTime completedAt, string resultsPath);
    }
}
