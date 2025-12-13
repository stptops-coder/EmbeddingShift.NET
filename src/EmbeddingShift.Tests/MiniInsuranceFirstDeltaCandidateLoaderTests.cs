using System;
using System.IO;
using System.Text;
using System.Text.Json;
using EmbeddingShift.ConsoleEval;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that MiniInsuranceFirstDeltaCandidateLoader picks the
    /// latest training run and returns its Delta vector.
    /// </summary>
    public class MiniInsuranceFirstDeltaCandidateLoaderTests
    {
        [Fact]
        public void LoadLatestDeltaVectorOrDefault_UsesNewestTrainingRun()
        {
            var baseDir = Path.Combine(
                Path.GetTempPath(),
                "MiniInsuranceFirstDeltaCandidateLoaderTests_" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(baseDir);

            try
            {
                // Older run
                CreateTrainingRun(
                    baseDir,
                    "mini-insurance-first-delta-training_20250101_000000_000",
                    new float[] { 0f, 0f, 0.3f });

                // Newer run
                CreateTrainingRun(
                    baseDir,
                    "mini-insurance-first-delta-training_20250102_000000_000",
                    new float[] { 0f, 0f, 0.7f });

                // Act
                var vector = MiniInsuranceFirstDeltaCandidateLoader
                    .LoadLatestDeltaVectorOrDefault(baseDir, out var found);

                // Assert
                Assert.True(found);
                Assert.True(vector.Length >= 3);
                Assert.Equal(0.7f, vector[2], 5);
            }
            finally
            {
                try
                {
                    Directory.Delete(baseDir, recursive: true);
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }

        private static void CreateTrainingRun(
            string baseDir,
            string folderName,
            float[] deltaVector)
        {
            var runDir = Path.Combine(baseDir, folderName);
            Directory.CreateDirectory(runDir);

            var candidate = new MiniInsuranceShiftTrainingResult
            {
                CreatedUtc = DateTime.UtcNow,
                BaseDirectory = baseDir,
                ComparisonRuns = 1,
                ImprovementFirst = 0.0,
                ImprovementFirstPlusDelta = 0.0,
                DeltaVector = deltaVector
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var jsonPath = Path.Combine(runDir, "shift-candidate.json");
            var json = JsonSerializer.Serialize(candidate, options);
            File.WriteAllText(jsonPath, json, encoding);
        }
    }
}
