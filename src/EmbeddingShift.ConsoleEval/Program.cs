using EmbeddingShift.Abstractions;
using EmbeddingShift.ConsoleEval;
using EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Workflows;
using System.Data;

// Composition Root (kept simple)
IRunLogger logger = new ConsoleRunLogger();
var runner = EvaluationRunner.WithDefaults(logger);

// Demo ingest components
IIngestor ingestor = new MinimalTxtIngestor();
IEmbeddingProvider provider = new SimEmbeddingProvider();

// Dummy vector store for demo (does not persist – only wiring test)
// IVectorStore store = new NoopStore();
IVectorStore store = new EmbeddingShift.Core.Persistence.FileStore(Path.Combine(AppContext.BaseDirectory, "data"));

var ingestWf = new IngestWorkflow(ingestor, provider, store);
var evalWf = new EvaluationWorkflow(runner);

// --- CLI ---
if (args.Length == 0)
{
    PrintHelp();
    return;
}

switch (args[0].ToLowerInvariant())
{
    case "ingest":
        // usage: ingest <path> <dataset>
        var input = args.Length >= 3
            ? args[1]
            : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples", "demo");

        var dataset = args.Length >= 3 ? args[2] : "DemoDataset";

        await ingestWf.RunAsync(input, dataset);
        Console.WriteLine("Ingest finished.");
        break;

    case "eval":
        // usage: eval <dataset>
        // For demo we build queries/references artificially
        var q1 = await provider.GetEmbeddingAsync("query one");
        var q2 = await provider.GetEmbeddingAsync("query two");
        var r1 = await provider.GetEmbeddingAsync("answer one");
        var r2 = await provider.GetEmbeddingAsync("answer two");
        var queries = new List<ReadOnlyMemory<float>> { q1, q2 };
        var refs = new List<ReadOnlyMemory<float>> { r1, r2 };

        // No real shift → identity via NullShift
        IShift shift = new NullShift();

        evalWf.Run(shift, queries, refs, args.Length >= 2 ? args[1] : "DemoDataset");
        break;

    default:
        PrintHelp();
        break;
}

static void PrintHelp()
{
    Console.WriteLine("RakeX CLI (simple)");
    Console.WriteLine("  ingest <path> <dataset>   - ingest TXT lines (demo)");
    Console.WriteLine("  eval   <dataset>          - evaluate with simulated embeddings (demo)");
}

// --- Helper objects for the demo ---
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
