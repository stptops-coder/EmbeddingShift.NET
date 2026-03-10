using System;
using System.IO;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.ConsoleEval.Repositories;
using Xunit;

namespace EmbeddingShift.Tests;

public sealed class ShiftTrainingResultRepositoryTests
{
    [Fact]
    public void LoadBest_prefers_selection_score_when_present()
    {
        var root = CreateTempRoot();
        try
        {
            var repository = new FileSystemShiftTrainingResultRepository(root);

            repository.Save(new ShiftTrainingResult
            {
                WorkflowName = "mini-insurance-posneg",
                CreatedUtc = new DateTime(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc),
                BaseDirectory = root,
                ComparisonRuns = 10,
                ImprovementFirst = 0.0,
                ImprovementFirstPlusDelta = 0.0,
                DeltaImprovement = 0.0,
                SelectionScore = 0.010,
                SelectionMapAt1Baseline = 0.050,
                SelectionMapAt1Shifted = 0.060,
                SelectionNdcg3Baseline = 0.020,
                SelectionNdcg3Shifted = 0.025,
                DeltaVector = new[] { 1.0f },
                ScopeId = "default"
            });

            repository.Save(new ShiftTrainingResult
            {
                WorkflowName = "mini-insurance-posneg",
                CreatedUtc = new DateTime(2026, 3, 9, 10, 5, 0, DateTimeKind.Utc),
                BaseDirectory = root,
                ComparisonRuns = 10,
                ImprovementFirst = 0.0,
                ImprovementFirstPlusDelta = 0.0,
                DeltaImprovement = 0.0,
                SelectionScore = 0.005,
                SelectionMapAt1Baseline = 0.050,
                SelectionMapAt1Shifted = 0.055,
                SelectionNdcg3Baseline = 0.020,
                SelectionNdcg3Shifted = 0.023,
                DeltaVector = new[] { 2.0f },
                ScopeId = "default"
            });

            var best = repository.LoadBest("mini-insurance-posneg");

            Assert.NotNull(best);
            Assert.Equal(0.010, best!.SelectionScore.GetValueOrDefault(), 6);
            Assert.Equal(new DateTime(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc), best.CreatedUtc);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "EmbeddingShift.Tests",
            nameof(ShiftTrainingResultRepositoryTests),
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDelete(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}
