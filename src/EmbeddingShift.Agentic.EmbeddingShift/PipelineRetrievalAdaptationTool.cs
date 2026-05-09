using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.Agentic;

namespace EmbeddingShift.Agentic.EmbeddingShift;

/// <summary>
/// Adapter that exposes an IEmbeddingShiftPipeline as a retrieval adaptation tool.
/// The implementation is deterministic and local: it shifts the query embedding,
/// ranks candidates by cosine similarity, and returns baseline vs. adapted rankings.
/// </summary>
public sealed class PipelineRetrievalAdaptationTool : IRetrievalAdaptationTool
{
    private readonly IEmbeddingShiftPipeline _shiftPipeline;

    public PipelineRetrievalAdaptationTool(IEmbeddingShiftPipeline shiftPipeline)
    {
        _shiftPipeline = shiftPipeline ?? throw new ArgumentNullException(nameof(shiftPipeline));
    }

    public Task<RetrievalAdaptationResult> AdaptAsync(
        RetrievalAdaptationRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.QueryEmbedding is null)
        {
            throw new ArgumentException("Query embedding must not be null.", nameof(request));
        }

        if (request.Candidates is null)
        {
            throw new ArgumentException("Candidates must not be null.", nameof(request));
        }

        ct.ThrowIfCancellationRequested();

        var topK = Math.Max(1, request.TopK);
        var queryEmbedding = Copy(request.QueryEmbedding);
        var adaptedQueryEmbedding = Copy(request.QueryEmbedding);

        _shiftPipeline.ApplyInPlace(adaptedQueryEmbedding);

        var baseline = Rank(queryEmbedding, request.Candidates, topK, ct);
        var adapted = Rank(adaptedQueryEmbedding, request.Candidates, topK, ct);

        var result = new RetrievalAdaptationResult(
            request.Query,
            baseline,
            adapted,
            _shiftPipeline.Shifts.Count);

        return Task.FromResult(result);
    }

    private static IReadOnlyList<RankedRetrievalCandidate> Rank(
        float[] queryEmbedding,
        IReadOnlyList<RetrievalCandidate> candidates,
        int topK,
        CancellationToken ct)
    {
        return candidates
            .Select(candidate =>
            {
                ct.ThrowIfCancellationRequested();

                if (candidate.Embedding is null)
                {
                    throw new ArgumentException($"Candidate '{candidate.Id}' has no embedding.", nameof(candidates));
                }

                if (candidate.Embedding.Length != queryEmbedding.Length)
                {
                    throw new ArgumentException(
                        $"Candidate '{candidate.Id}' embedding length does not match the query embedding length.",
                        nameof(candidates));
                }

                return new
                {
                    Candidate = candidate,
                    Score = Cosine(queryEmbedding, candidate.Embedding)
                };
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Candidate.Id, StringComparer.OrdinalIgnoreCase)
            .Take(topK)
            .Select((x, index) => new RankedRetrievalCandidate(
                x.Candidate.Id,
                x.Candidate.Text,
                x.Score,
                index + 1))
            .ToArray();
    }

    private static float[] Copy(float[] source)
    {
        var copy = new float[source.Length];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    private static double Cosine(float[] left, float[] right)
    {
        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        if (leftNorm <= 0 || rightNorm <= 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
    }
}
