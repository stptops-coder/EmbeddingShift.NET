using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using EmbeddingShift.Core.Runs;
using EmbeddingShift.Core.Stats;
using Xunit;

namespace EmbeddingShift.Tests
{
    public sealed class RunBestSelectionTests
    {
        [Fact]
        public void SelectBest_PicksHighestMetric()
        {
            var root = Path.Combine(Path.GetTempPath(), "EmbeddingShiftTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                var d1 = Path.Combine(root, "r1");
                var d2 = Path.Combine(root, "r2");
                Directory.CreateDirectory(d1);
                Directory.CreateDirectory(d2);

                var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

                var a = new WorkflowRunArtifact(
                    RunId: "r1",
                    WorkflowName: "w1",
                    StartedUtc: DateTimeOffset.UtcNow,
                    FinishedUtc: DateTimeOffset.UtcNow,
                    Success: true,
                    Metrics: new Dictionary<string, double> { ["ndcg@3"] = 0.10 },
                    Notes: "");

                var b = new WorkflowRunArtifact(
                    RunId: "r2",
                    WorkflowName: "w2",
                    StartedUtc: DateTimeOffset.UtcNow,
                    FinishedUtc: DateTimeOffset.UtcNow,
                    Success: true,
                    Metrics: new Dictionary<string, double> { ["ndcg@3"] = 0.30 },
                    Notes: "");

                File.WriteAllText(Path.Combine(d1, "run.json"), JsonSerializer.Serialize(a, opts), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(d2, "run.json"), JsonSerializer.Serialize(b, opts), new UTF8Encoding(false));

                var discovered = RunArtifactDiscovery.Discover(root);
                var best = RunBestSelection.SelectBest("ndcg@3", discovered);

                Assert.NotNull(best);
                Assert.Equal("w2", best!.Run.Artifact.WorkflowName);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { /* ignore */ }
            }
        }
    }
}
