using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Core.Infrastructure;
using EmbeddingShift.Workflows;
using EmbeddingShift.Workflows.Eval;
using EmbeddingShift.Workflows.Ingest;
using EmbeddingShift.Workflows.Run;
using System;

namespace EmbeddingShift.ConsoleEval;

public static class ConsoleEvalComposition
{
    public static ConsoleEvalServices CreateServices(ConsoleEvalGlobalOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        // Base provider is controlled via environment (EMBEDDING_BACKEND + sim/cache vars).
        IEmbeddingProvider baseProvider = EmbeddingProviderFactory.FromEnvironment();
        EmbeddingConsoleDiagnostics.PrintEmbeddingConfiguration();

        IEmbeddingProvider provider = (options.Provider ?? "sim").Trim().ToLowerInvariant() switch
        {
            "openai-echo" => new EmbeddingShift.Providers.OpenAI.EchoEmbeddingProvider(baseProvider),
            "openai-dryrun" => new EmbeddingShift.Providers.OpenAI.DryRunEmbeddingProvider(baseProvider),
            _ => baseProvider
        };

        Console.WriteLine($"[BOOT] Embedding provider = {provider.Name}");

        IRunLogger logger = new ConsoleRunLogger();
        var runner = EvaluationRunner.WithDefaults(logger);

        // File-based store rooted under data/
        var storeRoot = DirectoryLayout.ResolveDataRoot();
        IVectorStore store = new EmbeddingShift.Core.Persistence.FileStore(storeRoot);

        // Canonical ingest entrypoint (domain-neutral; reusable for CLI and future UI).
        var ingestEntry = new DatasetIngestEntry(provider, store);
        var ingestDatasetEntry = new DatasetIngestDatasetEntry(ingestEntry);

        // Ingestors used by CLI commands
        var txtLineIngestor = new MinimalTxtIngestor();
        var queriesJsonIngestor = new JsonQueryIngestor();

        var evalWf = new EvaluationWorkflow(runner);
        var evalEntry = new DatasetEvalEntry(provider, evalWf);
        var runEntry = new DatasetRunEntry(ingestDatasetEntry, evalEntry);

        return new ConsoleEvalServices(
            Method: options.Method,
            IngestEntry: ingestEntry,
            IngestDatasetEntry: ingestDatasetEntry,
            EvalEntry: evalEntry,
            RunEntry: runEntry,
            TxtLineIngestor: txtLineIngestor,
            QueriesJsonIngestor: queriesJsonIngestor);
    }
}
