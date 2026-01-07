using System;
using System.IO;
using System.Text;
using System.Text.Json;
using EmbeddingShift.Core.Runs;
using Xunit;

namespace EmbeddingShift.Tests
{
    public sealed class RunActivationHistoryTests
    {
        [Fact]
        public void ListHistory_SortsAndCanExcludePreRollback()
        {
            var root = Path.Combine(Path.GetTempPath(), "EmbeddingShiftTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                var historyDir = Path.Combine(root, "_active", "history");
                Directory.CreateDirectory(historyDir);

                var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };

                ActiveRunPointer Make(string runId, double score) =>
                    new ActiveRunPointer(
                        MetricKey: "ndcg@3",
                        CreatedUtc: DateTimeOffset.UtcNow,
                        RunsRoot: root,
                        TotalRunsFound: 3,
                        WorkflowName: "wf-" + runId,
                        RunId: runId,
                        Score: score,
                        RunDirectory: Path.Combine(root, runId),
                        RunJsonPath: Path.Combine(root, runId, "run.json"));

                var f1 = Path.Combine(historyDir, "active_ndcg@3_20260107_000001_000.json");
                var f2 = Path.Combine(historyDir, "active_ndcg@3_preRollback_20260107_000002_000.json");
                var f3 = Path.Combine(historyDir, "active_ndcg@3_20260107_000003_000.json");

                File.WriteAllText(f1, JsonSerializer.Serialize(Make("r1", 0.1), opts), new UTF8Encoding(false));
                File.WriteAllText(f2, JsonSerializer.Serialize(Make("r2", 0.2), opts), new UTF8Encoding(false));
                File.WriteAllText(f3, JsonSerializer.Serialize(Make("r3", 0.3), opts), new UTF8Encoding(false));

                // deterministic ordering via timestamps
                File.SetLastWriteTimeUtc(f1, new DateTime(2026, 01, 07, 0, 0, 1, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(f2, new DateTime(2026, 01, 07, 0, 0, 2, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(f3, new DateTime(2026, 01, 07, 0, 0, 3, DateTimeKind.Utc));

                var all = RunActivation.ListHistory(root, "ndcg@3", maxItems: 10, includePreRollback: true);
                Assert.Equal(3, all.Count);
                Assert.True(all[0].LastWriteUtc >= all[1].LastWriteUtc);

                var noPre = RunActivation.ListHistory(root, "ndcg@3", maxItems: 10, includePreRollback: false);
                Assert.Equal(2, noPre.Count);
                Assert.DoesNotContain(noPre, e => e.IsPreRollback);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { /* ignore */ }
            }
        }
    }
}
