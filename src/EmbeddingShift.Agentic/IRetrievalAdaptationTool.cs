using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddingShift.Agentic;

/// <summary>
/// Retrieval adaptation boundary that can be called from a RAG pipeline,
/// workflow controller, or agentic controller before downstream model reasoning.
/// It does not perform model reasoning itself.
/// </summary>
public interface IRetrievalAdaptationTool
{
    Task<RetrievalAdaptationResult> AdaptAsync(
        RetrievalAdaptationRequest request,
        CancellationToken ct = default);
}

public sealed record RetrievalAdaptationRequest(
    string Query,
    float[] QueryEmbedding,
    IReadOnlyList<RetrievalCandidate> Candidates,
    int TopK = 3);

public sealed record RetrievalCandidate(
    string Id,
    string Text,
    float[] Embedding);

public sealed record RetrievalAdaptationResult(
    string Query,
    IReadOnlyList<RankedRetrievalCandidate> BaselineRanking,
    IReadOnlyList<RankedRetrievalCandidate> AdaptedRanking,
    int AppliedShiftCount);

public sealed record RankedRetrievalCandidate(
    string Id,
    string Text,
    double Score,
    int Rank);
