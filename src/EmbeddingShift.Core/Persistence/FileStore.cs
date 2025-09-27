using System.Text.Json;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Persistence;

public sealed class FileStore : IVectorStore
{
    private readonly string _root;
    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public FileStore(string root)
    {
        _root = root;
        Directory.CreateDirectory(Path.Combine(_root, "embeddings"));
        Directory.CreateDirectory(Path.Combine(_root, "shifts"));
        Directory.CreateDirectory(Path.Combine(_root, "runs"));
    }

    public async Task SaveEmbeddingAsync(Guid id, float[] vector, string space, string provider, int dimensions)
    {
        var rec = new { id, space, provider, dimensions, vector };
        var path = Path.Combine(_root, "embeddings", $"{id:N}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(rec, J));
    }

    public async Task<float[]> LoadEmbeddingAsync(Guid id)
    {
        var path = Path.Combine(_root, "embeddings", $"{id:N}.json");
        if (!File.Exists(path)) return Array.Empty<float>();
        var json = await File.ReadAllTextAsync(path);
        var rec = JsonSerializer.Deserialize<EmbeddingRec>(json, J);
        return rec?.vector ?? Array.Empty<float>();
    }

    public async Task SaveShiftAsync(Guid id, string type, string parametersJson)
    {
        var rec = new { id, type, parameters = parametersJson, savedAt = DateTime.UtcNow };
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
        var rec = new { runId, kind, dataset, startedAt, completedAt, resultsPath };
        var path = Path.Combine(_root, "runs", $"{runId:N}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(rec, J));
    }

    private sealed record EmbeddingRec(Guid id, string space, string provider, int dimensions, float[] vector);
    private sealed record ShiftRec(Guid id, string type, string parameters);
}
