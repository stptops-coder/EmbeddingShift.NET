using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddingShift.Core.Infrastructure
{
    /// <summary>
    /// Authoritative "what is currently persisted for this space?" marker.
    /// Stored alongside persisted embeddings so eval can reliably resolve lineage.
    /// </summary>
    public sealed record EmbeddingSpaceState(
        string Space,
        string Mode,
        bool UsedJson,
        string Provider,
        DateTime CreatedUtc,
        string? ChunkFirstManifestPath = null);

    public static class EmbeddingSpaceStateStore
    {
        private const string StateFileName = "space_state_latest.json";

        private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        public static string ResolveStatePath(string embeddingsRoot, string space)
        {
            return Path.Combine(
                embeddingsRoot,
                SpacePath.ToRelativePath(space),
                StateFileName);
        }

        public static async Task TryWriteAsync(string embeddingsRoot, EmbeddingSpaceState state, CancellationToken ct = default)
        {
            if (state is null) return;
            if (string.IsNullOrWhiteSpace(embeddingsRoot)) return;

            try
            {
                var path = ResolveStatePath(embeddingsRoot, state.Space);
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(state, J);
                await File.WriteAllTextAsync(path, json, new UTF8Encoding(false), ct);
            }
            catch
            {
                // state is best-effort; never break ingest/eval because of it
            }
        }

        public static EmbeddingSpaceState? TryRead(string embeddingsRoot, string space)
        {
            if (string.IsNullOrWhiteSpace(embeddingsRoot) || string.IsNullOrWhiteSpace(space))
                return null;

            try
            {
                var path = ResolveStatePath(embeddingsRoot, space);
                if (!File.Exists(path)) return null;

                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<EmbeddingSpaceState>(json, J);
            }
            catch
            {
                return null;
            }
        }
    }
}
