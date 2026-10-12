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

    [Theory]
    [InlineData("--runid=")]
    [InlineData("--runid=   ")]
    [InlineData("--runid")]
    [InlineData("--runid=\"\"")]
    public async Task EmptyExplicitRunId_DoesNotReplaceTheActiveRun(string option)
    {
        using var runs = new RunFixture();
        runs.Write("baseline", "active-run", 0.2);
        var active = RunActivation.Promote(runs.Root, "ndcg@3");
        var originalPointer = File.ReadAllBytes(active.ActivePath);
        runs.Write("better", "better-run", 0.8);

        var originalExitCode = Environment.ExitCode;
        try
        {
            await RunsPromoteCommand.RunAsync(new[]
            {
                "--runs-root", runs.Root, "--metric", "ndcg@3", option
            });

            Assert.Equal(1, Environment.ExitCode);
            Assert.Equal(originalPointer, File.ReadAllBytes(active.ActivePath));
            Assert.Empty(RunActivation.ListHistory(runs.Root, "ndcg@3"));
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
        }
    }

    [Theory]
    [InlineData("--rank=1", "repo-run")]
    [InlineData("--rank=2", "normal-run")]
    [InlineData("--runid=repo-run", "repo-run")]
    [InlineData("--runid=normal-run", "normal-run")]
    public async Task ExplicitSelection_WithRepoInclusion_UsesTheCombinedCandidates(
        string option, string expectedRunId)
    {
        using var runs = new RunFixture();
        runs.Write("normal", "normal-run", 0.2);
        runs.Write("posneg", "repo-run", 0.8, inRepo: true);
        runs.Write("failed", "failed-run", 0.99, success: false, inRepo: true);

        var originalExitCode = Environment.ExitCode;
        try
        {
            await RunsPromoteCommand.RunAsync(new[]
            {
                "--runs-root", runs.Root, "--metric", "ndcg@3", "--profile", "combined",
                "--include-repo-posneg", option
            });

            Assert.Equal(0, Environment.ExitCode);
            Assert.True(RunActivation.TryLoadActive(runs.Root, "ndcg@3", "combined", out var active));
            Assert.Equal(expectedRunId, active!.RunId);
            Assert.Equal(3, active.TotalRunsFound);
            Assert.False(RunActivation.TryLoadActive(runs.Root, "ndcg@3", out _));
        }
        finally
        {
            Environment.ExitCode = originalExitCode;
        }
    }

    [Theory]
    [InlineData("--rank=3")]
    [InlineData("--runid=failed-run")]
    public async Task InvalidRepoSelection_DoesNotChangeTheActiveRun(string option)
    {
        using var runs = new RunFixture();
        runs.Write("normal", "normal-run", 0.2);
        var active = RunActivation.Promote(runs.Root, "ndcg@3");
        var originalPointer = File.ReadAllBytes(active.ActivePath);
        runs.Write("posneg", "repo-run", 0.8, inRepo: true);
        runs.Write("failed", "failed-run", 0.99, success: false, inRepo: true);

        var originalExitCode = Environment.ExitCode;
        try
        {
            await RunsPromoteCommand.RunAsync(new[]
            {
                "--runs-root", runs.Root, "--metric", "ndcg@3", "--include-repo-posneg", option
            });

            Assert.Equal(1, Environment.ExitCode);
            Assert.Equal(originalPointer, File.ReadAllBytes(active.ActivePath));
            Assert.Empty(RunActivation.ListHistory(runs.Root, "ndcg@3"));
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

        public void Write(string workflow, string runId, double score, bool success = true, bool inRepo = false)
        {
            var directory = inRepo
                ? Path.Combine(Root, "_repo", "MiniInsurance-PosNeg", workflow)
                : Path.Combine(Root, workflow);
            Directory.CreateDirectory(directory);
            var artifact = new WorkflowRunArtifact(runId, workflow,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, success,
                new Dictionary<string, double> { ["ndcg@3"] = score }, "");
            File.WriteAllText(Path.Combine(directory, "run.json"),
                JsonSerializer.Serialize(artifact, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
