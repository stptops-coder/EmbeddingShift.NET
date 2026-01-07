using EmbeddingShift.Core.Runs;
using Xunit;

namespace EmbeddingShift.Tests;

public sealed class RunMatrixSpecTests
{
    [Fact]
    public void Load_MinimalSpec_Works()
    {
        var json = """
        {
          "variants": [
            { "name": "v1", "args": [ "--help" ] }
          ]
        }
        """;

        var tempDir = Path.Combine(Path.GetTempPath(), "EmbeddingShift.Tests", "matrix", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var path = Path.Combine(tempDir, "matrix.json");
        File.WriteAllText(path, json);

        var spec = RunMatrixSpec.Load(path);

        Assert.NotNull(spec);
        Assert.Single(spec.Variants);
        Assert.Equal("v1", spec.Variants[0].Name);
        Assert.Equal(new[] { "--help" }, spec.Variants[0].Args);
        Assert.True(spec.StopOnFailure);
        Assert.Null(spec.After);
    }

    [Fact]
    public void Load_WithAfter_UsesDefaults()
    {
        var json = """
        {
          "variants": [
            { "name": "v1", "args": [ "--help" ] }
          ],
          "after": {
            "compareMetric": "ndcg@3",
            "top": 5,
            "writeCompare": true,
            "promoteBest": false,
            "openOutput": false,
            "domainKey": "insurance"
          }
        }
        """;

        var tempDir = Path.Combine(Path.GetTempPath(), "EmbeddingShift.Tests", "matrix", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var path = Path.Combine(tempDir, "matrix.json");
        File.WriteAllText(path, json);

        var spec = RunMatrixSpec.Load(path);

        Assert.NotNull(spec.After);
        Assert.Equal("ndcg@3", spec.After!.CompareMetric);
        Assert.Equal(5, spec.After.Top);
        Assert.True(spec.After.WriteCompare);
        Assert.False(spec.After.PromoteBest);
        Assert.False(spec.After.OpenOutput);
        Assert.Equal("insurance", spec.After.DomainKey);
    }
}
