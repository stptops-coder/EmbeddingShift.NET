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
    public sealed class RunArtifactDiscoveryTests
    {
        [Fact]
        public void Discover_LoadsRunJsonRecursively()
        {
            var root = Path.Combine(Path.GetTempPath(), "EmbeddingShiftTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                var aDir = Path.Combine(root, "a");
                var bDir = Path.Combine(root, "b", "c");
                Directory.CreateDirectory(aDir);
                Directory.CreateDirectory(bDir);

                var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                };

                var a = new WorkflowRunArtifact(
                    RunId: "r1",
                    WorkflowName: "w1",
                    StartedUtc: DateTimeOffset.UtcNow,
                    FinishedUtc: DateTimeOffset.UtcNow,
                    Success: true,
                    Metrics: new Dictionary<string, double> { ["ndcg@3"] = 0.10, ["map@1"] = 0.20 },
                    Notes: "n1");

                var b = new WorkflowRunArtifact(
                    RunId: "r2",
                    WorkflowName: "w2",
                    StartedUtc: DateTimeOffset.UtcNow,
                    FinishedUtc: DateTimeOffset.UtcNow,
                    Success: true,
                    Metrics: new Dictionary<string, double> { ["ndcg@3"] = 0.30, ["map@1"] = 0.40 },
                    Notes: "n2");

                File.WriteAllText(Path.Combine(aDir, "run.json"), JsonSerializer.Serialize(a, opts), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(bDir, "run.json"), JsonSerializer.Serialize(b, opts), new UTF8Encoding(false));

                var found = RunArtifactDiscovery.Discover(root);

                Assert.Equal(2, found.Count);
                Assert.Contains(found, x => x.Artifact.WorkflowName == "w1");
                Assert.Contains(found, x => x.Artifact.WorkflowName == "w2");

                Assert.True(RunArtifactDiscovery.TryGetMetric(found[0].Artifact, "ndcg@3", out var _));
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { /* ignore */ }
            }
        }
    }
}
