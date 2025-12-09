using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Utils;
using EmbeddingShift.Workflows;

namespace EmbeddingShift.ConsoleEval;

/// <summary>
/// Tiny demo that runs the AdaptiveWorkflow on synthetic vectors.
/// This keeps the demo independent from the file-based Mini-Insurance workflow.
/// </summary>
internal static class AdaptiveDemo
{
    public static void RunDemo(AdaptiveWorkflow workflow)
    {
        if (workflow == null) throw new ArgumentNullException(nameof(workflow));

        const int dim = EmbeddingDimensions.DIM;

        static float[] CreateVector(int dimension, float baseValue, float bias)
        {
            var v = new float[dimension];
            for (var i = 0; i < v.Length; i++)
            {
                v[i] = baseValue;
            }

            // Simple directional bias on the first dimension to make differences visible
            v[0] += bias;
            return v;
        }

        var query = new ReadOnlyMemory<float>(CreateVector(dim, 0.5f, 0.10f));

        var references = new List<ReadOnlyMemory<float>>
        {
            new(CreateVector(dim, 0.4f, 0.05f)),
            new(CreateVector(dim, 0.4f, -0.05f)),
            new(CreateVector(dim, 0.4f, 0.20f))
        };

        Console.WriteLine();
        Console.WriteLine("[Adaptive] Running synthetic demo with {0} references.", references.Count);

        var bestShift = workflow.Run(query, references);

        Console.WriteLine("[Adaptive] Selected shift: {0} (Kind={1})", bestShift.Name, bestShift.Kind);

        var baselineScores = ComputeCosineScores(query.Span, references);
        var shiftedQuery = bestShift.Apply(query.Span);
        var shiftedScores = ComputeCosineScores(shiftedQuery.Span, references);

        var baselineBestIndex = IndexOfMax(baselineScores);
        var shiftedBestIndex = IndexOfMax(shiftedScores);

        Console.WriteLine("[Adaptive] Baseline best index: {0}, score={1:F3}",
            baselineBestIndex, baselineScores[baselineBestIndex]);
        Console.WriteLine("[Adaptive] Shifted   best index: {0}, score={1:F3}",
            shiftedBestIndex, shiftedScores[shiftedBestIndex]);
    }

    private static float[] ComputeCosineScores(
        ReadOnlySpan<float> query,
        IReadOnlyList<ReadOnlyMemory<float>> references)
    {
        var scores = new float[references.Count];

        for (var i = 0; i < references.Count; i++)
        {
            scores[i] = VectorOps.Cosine(query, references[i].Span);
        }

        return scores;
    }

    private static int IndexOfMax(IReadOnlyList<float> scores)
    {
        var bestIndex = 0;
        var bestValue = float.NegativeInfinity;

        for (var i = 0; i < scores.Count; i++)
        {
            if (scores[i] > bestValue)
            {
                bestValue = scores[i];
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}
