using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using EmbeddingShift.Core.Stats;
using Xunit;

namespace EmbeddingShift.Tests
{
    public class RunRepositoryTests
    {
        [Fact]
        public async Task FileRunRepository_writes_run_artifact_as_json()
        {
            var baseDir = AppContext.BaseDirectory;
            var root = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
            var targetDir = Path.Combine(root, "results", "tests", "runs");

            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, recursive: true);
            }

            var repo = new FileRunRepository(targetDir);

            var metrics = new Dictionary<string, double>
            {
                ["test_metric"] = 1.23
            };

            var artifact = new WorkflowRunArtifact(
                RunId: "test-run",
                WorkflowName: "TestWorkflow",
                StartedUtc: DateTimeOffset.UtcNow,
                FinishedUtc: DateTimeOffset.UtcNow,
                Success: true,
                Metrics: metrics,
                Notes: "test run");

            await repo.SaveAsync(artifact, CancellationToken.None);

            Assert.True(Directory.Exists(targetDir));

            var runFiles = Directory.GetFiles(targetDir, "run.json", SearchOption.AllDirectories);
            Assert.Single(runFiles);

            var json = await File.ReadAllTextAsync(runFiles[0]);

            using var doc = JsonDocument.Parse(json);
            var root1 = doc.RootElement;

            // Check WorkflowName
            Assert.Equal("TestWorkflow", root1.GetProperty("WorkflowName").GetString());

            // Check metric payload
            var metrics1 = root1.GetProperty("Metrics");
            Assert.Equal(1.23, metrics1.GetProperty("test_metric").GetDouble());
        }
    }
}
