using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using EmbeddingShift.ConsoleEval;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that MiniInsuranceFirstDeltaAggregator correctly aggregates
    /// multiple metrics-comparison.json files.
    /// This protects the JSON shape and averaging logic against regressions.
    /// </summary>
    public class MiniInsuranceFirstDeltaAggregatorTests
    {
        [Fact]
        public void AggregateFromDirectory_ComputesExpectedAverages()
        {
            // Arrange: create an isolated temp directory.
            var baseDir = Path.Combine(Path.GetTempPath(),
                "MiniInsuranceFirstDeltaAggregatorTests_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(baseDir);

            try
            {
                // We create two comparison runs with slightly different values,
                // so that the averages are not trivial.

                // Run 1
                CreateComparisonRun(
                    baseDir,
                    "mini-insurance-first-delta_1",
                    new[]
                    {
                        new MiniInsuranceMetricRow
                        {
                            Metric = "map@1",
                            Baseline = 1.0,
                            First = 1.0,
                            FirstPlusDelta = 0.8,
                            DeltaFirstVsBaseline = 0.0,
                            DeltaFirstPlusDeltaVsBaseline = -0.2
                        },
                        new MiniInsuranceMetricRow
                        {
                            Metric = "ndcg@3",
                            Baseline = 0.9,
                            First = 1.0,
                            FirstPlusDelta = 0.95,
                            DeltaFirstVsBaseline = 0.1,
                            DeltaFirstPlusDeltaVsBaseline = 0.05
                        }
                    });

                // Run 2
                CreateComparisonRun(
                    baseDir,
                    "mini-insurance-first-delta_2",
                    new[]
                    {
                        new MiniInsuranceMetricRow
                        {
                            Metric = "map@1",
                            Baseline = 1.0,
                            First = 0.9,
                            FirstPlusDelta = 1.0,
                            DeltaFirstVsBaseline = -0.1,
                            DeltaFirstPlusDeltaVsBaseline = 0.0
                        },
                        new MiniInsuranceMetricRow
                        {
                            Metric = "ndcg@3",
                            Baseline = 0.8,
                            First = 0.8,
                            FirstPlusDelta = 0.8,
                            DeltaFirstVsBaseline = 0.0,
                            DeltaFirstPlusDeltaVsBaseline = 0.0
                        }
                    });

                // Act
                var aggregate = MiniInsuranceFirstDeltaAggregator.AggregateFromDirectory(baseDir);

                // Assert
                Assert.Equal(baseDir, aggregate.BaseDirectory);
                Assert.Equal(2, aggregate.ComparisonCount);

                var metrics = new Dictionary<string, MiniInsuranceAggregateMetricRow>();
                foreach (var row in aggregate.Metrics)
                {
                    metrics[row.Metric] = row;
                }

                Assert.True(metrics.ContainsKey("map@1"));
                Assert.True(metrics.ContainsKey("ndcg@3"));

                var map = metrics["map@1"];
                // For map@1:
                //   Baseline: (1.0 + 1.0) / 2 = 1.0
                //   First:    (1.0 + 0.9) / 2 = 0.95
                //   First+Δ:  (0.8 + 1.0) / 2 = 0.9
                //   ΔFirst:   (0.0 + -0.1) / 2 = -0.05
                //   ΔFirstΔ:  (-0.2 + 0.0) / 2 = -0.1
                Assert.Equal(1.0, map.AverageBaseline, 6);
                Assert.Equal(0.95, map.AverageFirst, 6);
                Assert.Equal(0.9, map.AverageFirstPlusDelta, 6);
                Assert.Equal(-0.05, map.AverageDeltaFirstVsBaseline, 6);
                Assert.Equal(-0.1, map.AverageDeltaFirstPlusDeltaVsBaseline, 6);

                var ndcg = metrics["ndcg@3"];
                // For ndcg@3:
                //   Baseline: (0.9 + 0.8) / 2 = 0.85
                //   First:    (1.0 + 0.8) / 2 = 0.9
                //   First+Δ:  (0.95 + 0.8) / 2 = 0.875
                //   ΔFirst:   (0.1 + 0.0) / 2 = 0.05
                //   ΔFirstΔ:  (0.05 + 0.0) / 2 = 0.025
                Assert.Equal(0.85, ndcg.AverageBaseline, 6);
                Assert.Equal(0.9, ndcg.AverageFirst, 6);
                Assert.Equal(0.875, ndcg.AverageFirstPlusDelta, 6);
                Assert.Equal(0.05, ndcg.AverageDeltaFirstVsBaseline, 6);
                Assert.Equal(0.025, ndcg.AverageDeltaFirstPlusDeltaVsBaseline, 6);
            }
            finally
            {
                // Best-effort cleanup; ignore exceptions.
                try
                {
                    Directory.Delete(baseDir, recursive: true);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private static void CreateComparisonRun(
            string baseDir,
            string folderName,
            IReadOnlyList<MiniInsuranceMetricRow> rows)
        {
            var runDir = Path.Combine(baseDir, folderName);
            Directory.CreateDirectory(runDir);

            var comparison = new MiniInsuranceFirstDeltaComparison
            {
                CreatedUtc = DateTime.UtcNow,
                WorkflowName = "mini-insurance-first-delta",
                Metrics = rows
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var jsonPath = Path.Combine(runDir, "metrics-comparison.json");
            var json = JsonSerializer.Serialize(comparison, options);
            File.WriteAllText(jsonPath, json, encoding);
        }
    }
}
