using System.Text.Json;
using System.Collections.Concurrent;
using System.Threading;
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
            WriteIndented = true
        };

// Prevent concurrent writers (e.g., parallel tests) from colliding on the same embedding file.
// Embeddings are deterministic for a given (space, provider, idKey, text), so a "first writer wins"
// policy is safe and avoids flaky IO exceptions on Windows.
private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathLocks =
    new(StringComparer.OrdinalIgnoreCase);

private static SemaphoreSlim GetPathLock(string path)
    => PathLocks.GetOrAdd(path, static _ => new SemaphoreSlim(1, 1));


        public FileStore(string root)
        {
            _root = root;

            // Note: subfolders are created lazily on first write, to keep startup/help side-effect free.
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

    // If the embedding already exists, treat it as immutable and skip rewriting.
    if (File.Exists(path))
        return;

    var json = JsonSerializer.Serialize(rec, J);

    var gate = GetPathLock(path);
    await gate.WaitAsync().ConfigureAwait(false);

    try
    {
        // Double-check once inside the gate.
        if (File.Exists(path))
            return;

        // Write to a temp file and move atomically to avoid partial reads on crashes.
        var tmp = path + ".tmp";

        try
        {
            // Small retry loop: Windows can transiently lock files during AV/indexing.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    await File.WriteAllTextAsync(tmp, json).ConfigureAwait(false);
                    File.Move(tmp, path, overwrite: true);
                    return;
                }
                catch (IOException) when (attempt < 2)
                {
                    await Task.Delay(15 * (attempt + 1)).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
            catch
            {
                // best effort cleanup only
            }
        }
    }
    finally
    {
        gate.Release();
    }
}

        private static string SpaceToPath(string space)
            => SpacePath.ToRelativePath(space);

                public async Task<float[]> LoadEmbeddingAsync(Guid id)
        {
            // Search in all space folders (space is not known at this callsite).
            var rootDir = Path.Combine(_root, "embeddings");
            if (!Directory.Exists(rootDir))
                return Array.Empty<float>();

            foreach (var file in Directory.EnumerateFiles(rootDir, $"{id:N}.json", SearchOption.AllDirectories))
            {
                var gate = GetPathLock(file);
                await gate.WaitAsync().ConfigureAwait(false);

                try
                {
                    // Small retry loop: Windows can transiently lock files during AV/indexing.
                    string? json = null;

                    for (var attempt = 0; attempt < 3; attempt++)
                    {
                        try
                        {
                            json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                            break;
                        }
                        catch (IOException) when (attempt < 2)
                        {
                            await Task.Delay(15 * (attempt + 1)).ConfigureAwait(false);
                        }
                        catch (FileNotFoundException)
                        {
                            // File disappeared between enumeration and read. Skip.
                            json = null;
                            break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(json))
                        continue;

                    var rec = JsonSerializer.Deserialize<EmbeddingRec>(json, J);
                    if (rec?.vector is { Length: > 0 })
                        return rec.vector;
                }
                finally
                {
                    gate.Release();
                }
            }

            return Array.Empty<float>();
        }

public async Task SaveShiftAsync(Guid id, string type, string parametersJson)
        {
            var rec = new ShiftRec(id, type, parametersJson, DateTime.UtcNow);

            Directory.CreateDirectory(Path.Combine(_root, "shifts"));
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

            Directory.CreateDirectory(Path.Combine(_root, "runs"));
            var path = Path.Combine(_root, "runs", $"{runId:N}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(rec, J));
        }

        private sealed record EmbeddingRec(Guid id, string space, string provider, int dimensions, float[] vector);
        private sealed record ShiftRec(Guid id, string type, string parameters, DateTime savedAt);
        private sealed record RunRec(Guid runId, string kind, string dataset, DateTime startedAt, DateTime completedAt, string resultsPath);
    }
}
