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
    public sealed class RunRollbackTests
    {
        [Fact]
        public void RollbackLatest_RestoresPreviousActive()
        {
            var root = Path.Combine(Path.GetTempPath(), "EmbeddingShiftTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                var r1 = Path.Combine(root, "r1");
                var r2 = Path.Combine(root, "r2");
                Directory.CreateDirectory(r1);
                Directory.CreateDirectory(r2);

                var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

                var a = new WorkflowRunArtifact(
                    RunId: "r1",
                    WorkflowName: "w1",
                    StartedUtc: DateTimeOffset.UtcNow,
                    FinishedUtc: DateTimeOffset.UtcNow.AddSeconds(1),
                    Success: true,
                    Metrics: new Dictionary<string, double> { ["ndcg@3"] = 0.10 },
                    Notes: "");

                var b = new WorkflowRunArtifact(
                    RunId: "r2",
                    WorkflowName: "w2",
                    StartedUtc: DateTimeOffset.UtcNow,
                    FinishedUtc: DateTimeOffset.UtcNow.AddSeconds(2),
                    Success: true,
                    Metrics: new Dictionary<string, double> { ["ndcg@3"] = 0.30 },
                    Notes: "");

                File.WriteAllText(Path.Combine(r1, "run.json"), JsonSerializer.Serialize(a, opts), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(r2, "run.json"), JsonSerializer.Serialize(b, opts), new UTF8Encoding(false));

                // First promote -> active = w2
                var first = RunActivation.Promote(root, "ndcg@3");
                Assert.Equal("w2", first.Pointer.WorkflowName);

                // Add a better run and promote again -> archive previous active (w2) to history
                var r3 = Path.Combine(root, "r3");
                Directory.CreateDirectory(r3);

                var c = new WorkflowRunArtifact(
                    RunId: "r3",
                    WorkflowName: "w3",
                    StartedUtc: DateTimeOffset.UtcNow,
                    FinishedUtc: DateTimeOffset.UtcNow.AddSeconds(3),
                    Success: true,
                    Metrics: new Dictionary<string, double> { ["ndcg@3"] = 0.50 },
                    Notes: "");

                File.WriteAllText(Path.Combine(r3, "run.json"), JsonSerializer.Serialize(c, opts), new UTF8Encoding(false));

                var second = RunActivation.Promote(root, "ndcg@3");
                Assert.Equal("w3", second.Pointer.WorkflowName);

                // Rollback should restore previous active (w2)
                var rollback = RunActivation.RollbackLatest(root, "ndcg@3");
                Assert.Equal("w2", rollback.Pointer.WorkflowName);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { /* ignore */ }
            }
        }
    }
}
