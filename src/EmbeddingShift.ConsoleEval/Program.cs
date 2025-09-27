using EmbeddingShift.Abstractions;
using EmbeddingShift.ConsoleEval;
using EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Workflows;

// Composition Root (kept simple)
IRunLogger logger = new ConsoleRunLogger();
var runner = EvaluationRunner.WithDefaults(logger);

// Demo ingest components
IIngestor ingestor = new MinimalTxtIngestor();
IEmbeddingProvider provider = new SimEmbeddingProvider();

// File-based vector store for persistence
IVectorStore store = new EmbeddingShift.Core.Persistence.FileStore(
    Path.Combine(AppContext.BaseDirectory, "data"));

var ingestWf = new IngestWorkflow(ingestor, provider, store);
var evalWf = new EvaluationWorkflow(runner);

// --- CLI ---
if (args.Length == 0)
{
    Helpers.PrintHelp();
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
                refs = Helpers.LoadVectorsBySpace(dataRoot, dataset);
                if (refs.Count == 0)
                {
                    Console.WriteLine($"No persisted embeddings found for dataset '{dataset}'.");
                    return;
                }

                // For demo: use the same set as queries.
                queries = refs.ToList();
                Console.WriteLine($"Eval mode: persisted embeddings (dataset '{dataset}'): {queries.Count} items.");
            }

            // No real shift yet → identity via NullShift
            IShift shift = new NullShift();
            evalWf.Run(shift, queries, refs, dataset);
            break;
        }

    default:
        Helpers.PrintHelp();
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
    public ReadOnlyMemory<float> Apply(ReadOnlySpan<float> input) => input.ToArray();
}

// --- Static helpers in class to avoid CS8803 ---
static class Helpers
{
    public static void PrintHelp()
    {
        Console.WriteLine("RakeX CLI (simple)");
        Console.WriteLine("  ingest <path> <dataset>   - ingest TXT lines (demo)");
        Console.WriteLine("  eval   <dataset> [--sim]  - evaluate with persisted or simulated embeddings (demo)");
    }

    public static List<ReadOnlyMemory<float>> LoadVectorsBySpace(string embeddingsDir, string space)
    {
        var result = new List<ReadOnlyMemory<float>>();
        if (!Directory.Exists(embeddingsDir)) return result;

        foreach (var file in Directory.EnumerateFiles(embeddingsDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(file);
                var rec = System.Text.Json.JsonSerializer.Deserialize<EmbeddingRec>(json);
                if (rec is null || rec.vector is null) continue;

                // Accept exact or "contains" match for safety
                if (string.Equals(rec.space, space, StringComparison.OrdinalIgnoreCase) ||
                    (rec.space?.IndexOf(space, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    result.Add(rec.vector);
                }
            }
            catch
            {
                // ignore unreadable files
            }
        }
        return result;
    }


    // Mirror of FileStore's record shape
    public sealed record EmbeddingRec(Guid id, string space, string provider, int dimensions, float[] vector);
}
