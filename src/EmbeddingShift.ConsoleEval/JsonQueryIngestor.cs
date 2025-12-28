using System.Text.Json;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.ConsoleEval
{
    /// <summary>
    /// Reads queries from queries.json (array of objects with a "text" field).
    /// Supports passing either a directory containing queries.json or the json file path itself.
    /// </summary>
    public sealed class JsonQueryIngestor : IIngestor
    {
        private sealed record QueryRec(string id, string text, string relevantDocId);

        public IEnumerable<(string Text, int Order)> Parse(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                yield break;

            var full = Path.GetFullPath(path);

            string jsonPath;
            if (Directory.Exists(full))
            {
                jsonPath = Path.Combine(full, "queries.json");
            }
            else
            {
                jsonPath = full;
            }

            if (!File.Exists(jsonPath))
                yield break;

            var json = File.ReadAllText(jsonPath);

            // tolerate BOM + standard formatting
            var items = JsonSerializer.Deserialize<List<QueryRec>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                        ?? new List<QueryRec>();

            var order = 0;
            foreach (var q in items)
            {
                if (!string.IsNullOrWhiteSpace(q.text))
                    yield return (q.text, order++);
            }
        }
    }
}
