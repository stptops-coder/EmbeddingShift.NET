using EmbeddingShift.Abstractions;
using EmbeddingShift.ConsoleEval;
using EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Workflows;

// Composition Root (einfach gehalten)
IRunLogger logger = new ConsoleRunLogger();
var runner = EvaluationRunner.WithDefaults(logger);

// Demo-Ingest-Komponenten (für „ingest“)
IIngestor ingestor = new MinimalTxtIngestor();
IEmbeddingProvider provider = new SimEmbeddingProvider();

// Dummy-VectorStore für die Demo (persistiert nicht – nur Kabeltest)
IVectorStore store = new NoopStore();

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
        if (args.Length < 3) { PrintHelp(); return; }
        await ingestWf.RunAsync(args[1], args[2]);
        Console.WriteLine("Ingest finished.");
        break;

    case "eval":
        // usage: eval <dataset>
        // Für Demo bauen wir Queries/References künstlich
        var q1 = await provider.GetEmbeddingAsync("query one");
        var q2 = await provider.GetEmbeddingAsync("query two");
        var r1 = await provider.GetEmbeddingAsync("answer one");
        var r2 = await provider.GetEmbeddingAsync("answer two");
        var queries = new List<ReadOnlyMemory<float>> { q1, q2 };
        var refs = new List<ReadOnlyMemory<float>> { r1, r2 };

        // Kein echter Shift → Identität via NullShift
        IShift shift = new NullShift();

        evalWf.Run(shift, queries, refs, args.Length >= 2 ? args[1] : "DemoDataset");
        break;

    default:
        PrintHelp();
        break;
}

static void PrintHelp()
{
    Console.WriteLine("RakeX CLI (einfach)");
    Console.WriteLine("  ingest <path> <dataset>   - TXT-Zeilen ingestieren (Demo)");
    Console.WriteLine("  eval   <dataset>          - Evaluierung mit Sim-Embeddings (Demo)");
}

// --- Hilfsobjekte für die Demo ---
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

// NullShift = Identität
sealed class NullShift : IShift
{
    public string Name => "NullShift";

    public ReadOnlyMemory<float> Apply(ReadOnlySpan<float> input) => input.ToArray();
}
