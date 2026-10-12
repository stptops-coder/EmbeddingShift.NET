using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Training.PosNeg;
using Xunit;

namespace EmbeddingShift.Tests;

public sealed class PosNegRankingRegressionTests
{
    [Theory]
    [InlineData(17)]
    [InlineData(32)]
    public async Task TiedScores_PreserveTheEvaluationRanking(int tiedNegatives)
    {
        // Stable evaluation ranks the relevant document first among equal scores.
        // A lower-scoring document precedes it in the original document order.
        var documents = new Dictionary<string, float[]>
        {
            ["lower"] = new[] { -1f, 0f },
            ["positive"] = new[] { 1f, 1f }
        };
        for (var i = 0; i < tiedNegatives; i++)
            documents.Add("negative" + i, new[] { 1f, -1f });

        var result = await PosNegDeltaVectorLearner.LearnAsync(
            new QueryProvider(),
            new[] { new PosNegTrainingQuery("q1", "query", "positive") },
            documents,
            new PosNegLearningOptions(1.5f, false, false));

        Assert.Equal(0, result.Stats.Cases);
        Assert.True(result.DeltaVector.All(value => value == 0f));
    }

    private sealed class QueryProvider : IEmbeddingProvider
    {
        public string Name => "ranking-regression";
        public Task<float[]> GetEmbeddingAsync(string text) =>
            Task.FromResult(new[] { 1f, 0f });
    }
}
