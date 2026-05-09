using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddingShift.Agentic;

/// <summary>
/// Replaceable downstream reasoning boundary. The default implementation is local
/// and deterministic; a real LLM client can be plugged in later without changing
/// the retrieval adaptation step.
/// </summary>
public interface IAgenticReasoningClient
{
    string Name { get; }
    bool IsSimulated { get; }

    Task<AgenticReasoningResult> GenerateAsync(
        AgenticReasoningRequest request,
        CancellationToken ct = default);
}

public sealed record AgenticReasoningRequest(
    string Query,
    IReadOnlyList<RankedRetrievalCandidate> RetrievedContext);

public sealed record AgenticReasoningResult(
    string Answer,
    string BackendName,
    bool IsSimulated,
    int EstimatedTokensIn,
    int EstimatedTokensOut);
