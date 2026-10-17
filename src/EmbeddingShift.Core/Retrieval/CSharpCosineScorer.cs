using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Retrieval;

/// <summary>Scores each document with the supplied cosine function.</summary>
public sealed class CSharpCosineScorer(Func<float[], float[], double> cosine) : ICosineScorer
{
    public Task<double[]> ScoreAsync(float[] query, IReadOnlyList<float[]> documents,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var scores = new double[documents.Count];
        for (var i = 0; i < scores.Length; i++)
            scores[i] = cosine(query, documents[i]);
        return Task.FromResult(scores);
    }

    public void Dispose() { }
}
