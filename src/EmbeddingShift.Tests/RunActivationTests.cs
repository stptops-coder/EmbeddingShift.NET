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
    public sealed class RunActivationTests
    {
        [Fact]
        public void Promote_WritesActiveAndArchivesPrevious()
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

                var first = RunActivation.Promote(root, "ndcg@3");
                Assert.True(File.Exists(first.ActivePath));
                Assert.NotNull(first.Pointer);
                Assert.Equal("w2", first.Pointer!.WorkflowName);

                // Add a better run (r3) and promote again -> previous active should be archived
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
                Assert.NotNull(second.Pointer);
                Assert.Equal("w3", second.Pointer!.WorkflowName);
                Assert.False(string.IsNullOrWhiteSpace(second.PreviousActiveArchivedTo));
                Assert.True(File.Exists(second.PreviousActiveArchivedTo!));
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { /* ignore */ }
            }
        }


        [Fact]
        public void Promote_ByRank_SelectsNthCandidate()
        {
            var root = Path.Combine(Path.GetTempPath(), "EmbeddingShiftTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

                void Write(string runId, string workflow, int order, double score)
                {
                    var dir = Path.Combine(root, runId);
                    Directory.CreateDirectory(dir);
                    var artifact = new WorkflowRunArtifact(
                        RunId: runId,
                        WorkflowName: workflow,
                        StartedUtc: DateTimeOffset.UtcNow,
                        FinishedUtc: DateTimeOffset.UtcNow.AddSeconds(order),
                        Success: true,
                        Metrics: new Dictionary<string, double> { ["ndcg@3"] = score },
                        Notes: "");
                    File.WriteAllText(Path.Combine(dir, "run.json"), JsonSerializer.Serialize(artifact, opts), new UTF8Encoding(false));
                }

                Write("r1", "w1", 1, 0.10);
                Write("r2", "w2", 2, 0.30);
                Write("r3", "w3", 3, 0.50);

                var picked = RunActivation.Promote(root, "ndcg@3", pickRank: 2, pickRunId: null);
                Assert.NotNull(picked.Pointer);
                Assert.Equal("w2", picked.Pointer!.WorkflowName);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { /* ignore */ }
            }
        }

        [Fact]
        public void Promote_ByRunId_SelectsSpecificRun()
        {
            var root = Path.Combine(Path.GetTempPath(), "EmbeddingShiftTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

                void Write(string runId, string workflow, int order, double score)
                {
                    var dir = Path.Combine(root, runId);
                    Directory.CreateDirectory(dir);
                    var artifact = new WorkflowRunArtifact(
                        RunId: runId,
                        WorkflowName: workflow,
                        StartedUtc: DateTimeOffset.UtcNow,
                        FinishedUtc: DateTimeOffset.UtcNow.AddSeconds(order),
                        Success: true,
                        Metrics: new Dictionary<string, double> { ["ndcg@3"] = score },
                        Notes: "");
                    File.WriteAllText(Path.Combine(dir, "run.json"), JsonSerializer.Serialize(artifact, opts), new UTF8Encoding(false));
                }

                Write("r1", "w1", 1, 0.10);
                Write("r2", "w2", 2, 0.30);

                var picked = RunActivation.Promote(root, "ndcg@3", pickRank: null, pickRunId: "r1");
                Assert.NotNull(picked.Pointer);
                Assert.Equal("w1", picked.Pointer!.WorkflowName);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { /* ignore */ }
            }
        }
    }
}
