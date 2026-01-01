using System;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Shifts;
using EmbeddingShift.Workflows.Eval;
using EmbeddingShift.Workflows.Ingest;
using EmbeddingShift.Workflows.Run;

namespace EmbeddingShift.ConsoleEval;

/// <summary>
/// UI-friendly facade over the canonical workflow entrypoints.
/// CLI remains an adapter; UI/services can call this host directly.
/// </summary>
public sealed class ConsoleEvalHost
{
    public ConsoleEvalGlobalOptions Options { get; }
    public ConsoleEvalServices Services { get; }

    private ConsoleEvalHost(ConsoleEvalGlobalOptions options, ConsoleEvalServices services)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Creates a host using the current process environment (EMBEDDING_* vars).
    /// If you want to apply global options to the environment, call
    /// ConsoleEvalGlobalEnvironment.Apply(options) BEFORE this.
    /// </summary>
    public static ConsoleEvalHost Create(ConsoleEvalGlobalOptions options)
    {
        var services = ConsoleEvalComposition.CreateServices(options);
        return new ConsoleEvalHost(options, services);
    }

    public Task<DatasetIngestDatasetResult> IngestDatasetAsync(
        DatasetIngestDatasetRequest request,
        CancellationToken ct = default)
    {
        return Services.IngestDatasetEntry.RunAsync(
            request: request,
            textLineIngestor: Services.TxtLineIngestor,
            queriesJsonIngestor: Services.QueriesJsonIngestor,
            ct: ct);
    }

    public Task<DatasetEvalResult> EvalAsync(
        IShift shift,
        DatasetEvalRequest request,
        CancellationToken ct = default)
    {
        return Services.EvalEntry.RunAsync(shift, request, ct);
    }

    public Task<DatasetRunResult> RunAsync(
        IShift shift,
        DatasetRunRequest request,
        CancellationToken ct = default)
    {
        return Services.RunEntry.RunAsync(
            shift: shift,
            request: request,
            textLineIngestor: Services.TxtLineIngestor,
            queriesJsonIngestor: Services.QueriesJsonIngestor,
            ct: ct);
    }

    /// <summary>
    /// Small convenience for host usage (kept intentionally minimal).
    /// </summary>
    public static IShift CreateShift(string? shiftId)
    {
        var id = (shiftId ?? "identity").Trim().ToLowerInvariant();

        return id switch
        {
            "zero" => new MultiplicativeShift(0f, EmbeddingDimensions.DIM),
            _ => new NoShiftIngestBased(), // identity
        };
    }
}
