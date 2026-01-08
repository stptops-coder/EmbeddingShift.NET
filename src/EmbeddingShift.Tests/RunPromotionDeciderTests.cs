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
    public sealed class RunPromotionDeciderTests
    {
        [Fact]
        public void Decide_NoActive_RecommendsPromote()
        {
            var root = Path.Combine(Path.GetTempPath(), "EmbeddingShift_RunsDecide_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(root);

                var r1 = Path.Combine(root, "r1");
                Directory.CreateDirectory(r1);

                var artifact = new WorkflowRunArtifact(
                    RunId: "r1",
                    WorkflowName: "w1",
                    StartedUtc: DateTimeOffset.UtcNow,
                    FinishedUtc: DateTimeOffset.UtcNow,
                    Success: true,
                    Metrics: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["ndcg@3"] = 0.5 },
                    Notes: "");

                var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
                File.WriteAllText(Path.Combine(r1, "run.json"), JsonSerializer.Serialize(artifact, opts), new UTF8Encoding(false));

                var decision = RunPromotionDecider.Decide(root, "ndcg@3", epsilon: 1e-6);

                Assert.Equal(RunPromotionDecisionAction.Promote, decision.Action);
                Assert.Null(decision.Active);
                Assert.Equal("w1", decision.Candidate.WorkflowName);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { /* ignore */ }
            }
        }

        [Fact]
        public void Decide_WithActive_UsesEpsilonGate()
        {
            var root = Path.Combine(Path.GetTempPath(), "EmbeddingShift_RunsDecide_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(root);

                // Two runs
                var r1 = Path.Combine(root, "r1");
                var r2 = Path.Combine(root, "r2");

                Directory.CreateDirectory(r1);
                Directory.CreateDirectory(r2);

                var a = new WorkflowRunArtifact(
                    RunId: "r1",
                    WorkflowName: "w1",
                    StartedUtc: DateTimeOffset.UtcNow,
                    FinishedUtc: DateTimeOffset.UtcNow,
                    Success: true,
                    Metrics: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["ndcg@3"] = 0.600000 },
                    Notes: "");

                var b = new WorkflowRunArtifact(
                    RunId: "r2",
                    WorkflowName: "w2",
                    StartedUtc: DateTimeOffset.UtcNow,
                    FinishedUtc: DateTimeOffset.UtcNow,
                    Success: true,
                    Metrics: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["ndcg@3"] = 0.600500 },
                    Notes: "");

                var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

                File.WriteAllText(Path.Combine(r1, "run.json"), JsonSerializer.Serialize(a, opts), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(r2, "run.json"), JsonSerializer.Serialize(b, opts), new UTF8Encoding(false));

                // Set active pointer manually to r1
                var activeDir = Path.Combine(root, "_active");
                Directory.CreateDirectory(activeDir);

                var pointer = new ActiveRunPointer(
                    MetricKey: "ndcg@3",
                    CreatedUtc: DateTimeOffset.UtcNow,
                    RunsRoot: root,
                    TotalRunsFound: 2,
                    WorkflowName: "w1",
                    RunId: "r1",
                    Score: 0.600000,
                    RunDirectory: r1,
                    RunJsonPath: Path.Combine(r1, "run.json"));

                var activePath = Path.Combine(activeDir, "active_ndcg@3.json");
                File.WriteAllText(activePath, JsonSerializer.Serialize(pointer, opts), new UTF8Encoding(false));

                // epsilon too high => keep active
                var keep = RunPromotionDecider.Decide(root, "ndcg@3", epsilon: 0.001);
                Assert.Equal(RunPromotionDecisionAction.KeepActive, keep.Action);

                // epsilon low => promote
                var promote = RunPromotionDecider.Decide(root, "ndcg@3", epsilon: 1e-6);
                Assert.Equal(RunPromotionDecisionAction.Promote, promote.Action);
                Assert.NotNull(promote.Active);
                Assert.Equal("w2", promote.Candidate.WorkflowName);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { /* ignore */ }
            }
        }
    }
}
