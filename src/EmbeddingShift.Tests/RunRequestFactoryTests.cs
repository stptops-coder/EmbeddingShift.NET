using EmbeddingShift.ConsoleEval;

namespace EmbeddingShift.Tests;

public sealed class RunRequestFactoryTests
{
    [Theory]
    [InlineData("EMBEDDINGSHIFT_DATASET_ROOT")]
    [InlineData("EMBEDDINGSHIFT_DATA_ROOT")]
    [InlineData("EMBEDDINGSHIFT_RESULTS_DOMAIN")]
    [InlineData("EMBEDDINGSHIFT_LAYOUT")]
    public void Create_CapturesDatasetAndStorageOverrides(string key)
    {
        var previous = Environment.GetEnvironmentVariable(key);
        try
        {
            Environment.SetEnvironmentVariable(key, "audit-override");
            var request = RunRequestFactory.Create(["domain", "mini-insurance", "pipeline"]);
            Assert.True(request.EnvironmentSnapshot!.TryGetValue(key, out var actual));
            Assert.Equal("audit-override", actual);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
        }
    }
}
