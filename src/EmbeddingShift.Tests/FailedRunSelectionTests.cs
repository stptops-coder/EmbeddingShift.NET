using System.Text.Json;
using EmbeddingShift.Core.Runs;
using EmbeddingShift.Core.Stats;

namespace EmbeddingShift.Tests;

public sealed class FailedRunSelectionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "EmbeddingShiftTests", Guid.NewGuid().ToString("N"));

    public FailedRunSelectionTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void SelectBest_ExcludesFailedRunWithHigherScore()
    {
        Write("passed", true, 0.25);
        Write("failed", false, 0.95);
        var best = RunBestSelection.SelectBest("ndcg@3", RunArtifactDiscovery.Discover(_root));
        Assert.Equal("passed", best?.Run.Artifact.RunId);
    }

    [Fact]
    public void SelectBest_ReturnsNullWhenOnlyFailedRunsExist()
    {
        Write("failed", false, 0.95);
        Assert.Null(RunBestSelection.SelectBest("ndcg@3", RunArtifactDiscovery.Discover(_root)));
    }

    [Fact]
    public void Promote_ExcludesFailedRunWithHigherScore()
    {
        Write("passed", true, 0.25);
        Write("failed", false, 0.95);
        Assert.Equal("passed", RunActivation.Promote(_root, "ndcg@3").Pointer.RunId);
    }

    [Fact]
    public void Promote_RejectsExplicitFailedRun()
    {
        Write("passed", true, 0.25);
        Write("failed", false, 0.95);
        Assert.Throws<InvalidOperationException>(() =>
            RunActivation.Promote(_root, "ndcg@3", pickRank: null, pickRunId: "failed"));
    }

    private void Write(string id, bool success, double score)
    {
        var dir = Path.Combine(_root, id);
        Directory.CreateDirectory(dir);
        var run = new WorkflowRunArtifact(id, "test", DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow, success, new Dictionary<string, double> { ["ndcg@3"] = score }, "");
        File.WriteAllText(Path.Combine(dir, "run.json"), JsonSerializer.Serialize(run));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
