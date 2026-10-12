using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EmbeddingShift.ConsoleEval.Commands;
using EmbeddingShift.Core.Runs;
using EmbeddingShift.Core.Stats;
using Xunit;

namespace EmbeddingShift.Tests;

public sealed class RunDecisionRegressionTests
{
    [Fact]
    public void DifferentWorkflowsWithSameRunId_CanPromoteTheBetterRun()
    {
        using var runs = new RunFixture();
        runs.Write("baseline", "shared-timestamp", 0.2);
        RunActivation.Promote(runs.Root, "ndcg@3");
        runs.Write("shifted", "shared-timestamp", 0.8);

        var decision = RunPromotionDecider.Decide(runs.Root, "ndcg@3");

        Assert.Equal(RunPromotionDecisionAction.Promote, decision.Action);
        Assert.Equal("shifted", decision.Candidate.WorkflowName);
        Assert.Equal(0.6, decision.Delta, precision: 12);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteEpsilon_IsRejected(double epsilon)
    {
        using var runs = new RunFixture();
        runs.Write("baseline", "run1", 0.2);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RunPromotionDecider.Decide(runs.Root, "ndcg@3", epsilon));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("invalid")]
    [InlineData("99999999999999999999")]
    [InlineData("")]
    public async Task InvalidExplicitRank_DoesNotActivateTheBestRun(string rank)
    {
        using var runs = new RunFixture();
        runs.Write("baseline", "run1", 0.2);
        var originalExitCode = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 0;
            await RunsPromoteCommand.RunAsync(new[]
            {
                "--runs-root", runs.Root, "--metric", "ndcg@3", "--rank=" + rank
            });

            Assert.Equal(1, Environment.ExitCode);
            Assert.False(RunActivation.TryLoadActive(runs.Root, "ndcg@3", out _));
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
        }
    }

    [Fact]
    public async Task RankWithoutValue_DoesNotActivateTheBestRun()
    {
        using var runs = new RunFixture();
        runs.Write("baseline", "run1", 0.2);
        var originalExitCode = Environment.ExitCode;
        try
        {
            Environment.ExitCode = 0;
            await RunsPromoteCommand.RunAsync(new[]
            {
                "--runs-root", runs.Root, "--metric", "ndcg@3", "--rank"
            });

            Assert.Equal(1, Environment.ExitCode);
            Assert.False(RunActivation.TryLoadActive(runs.Root, "ndcg@3", out _));
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
        }
    }

    private sealed class RunFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(),
            "EmbeddingShift_RunDecision_" + Guid.NewGuid().ToString("N"));

        public RunFixture() => Directory.CreateDirectory(Root);

        public void Write(string workflow, string runId, double score)
        {
            var directory = Path.Combine(Root, workflow);
            Directory.CreateDirectory(directory);
            var artifact = new WorkflowRunArtifact(runId, workflow,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true,
                new Dictionary<string, double> { ["ndcg@3"] = score }, "");
            File.WriteAllText(Path.Combine(directory, "run.json"),
                JsonSerializer.Serialize(artifact, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
