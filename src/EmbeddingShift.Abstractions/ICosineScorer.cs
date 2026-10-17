using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddingShift.Abstractions;

/// <summary>Scores one query against a document batch, preserving input order.</summary>
public interface ICosineScorer : IDisposable
{
    Task<double[]> ScoreAsync(float[] query, IReadOnlyList<float[]> documents,
        CancellationToken cancellationToken = default);
}
