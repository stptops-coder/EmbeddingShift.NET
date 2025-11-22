using System.IO;
using System.Threading.Tasks;
using EmbeddingShift.Core.Runs;
using EmbeddingShift.Core.Stats;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Full end-to-end integration test for the file-based insurance mini workflow.
    /// It runs the workflow via StatsAwareWorkflowRunner using the internal
    /// 1536D keyword-count embedding provider (local, deterministic, no API)
    /// and persists the result via RunPersistor.
    /// </summary>
    public class MiniInsuranceRealIntegrationTests
    {
        [Fact]
        public async Task File_based_insurance_workflow_can_be_persisted_with_valid_metrics()
        {
            // Arrange: file-based insurance workflow with local 1536D test embedding.
            IWorkflow workflow = new FileBasedInsuranceMiniWorkflow();
            var runner = new StatsAwareWorkflowRunner();

            // Act: execute workflow end-to-end.
            var artifacts = await runner.ExecuteAsync("FileBased-Insurance-Mini-IT", workflow);

            // Assert: metrics are present and in the expected range.
            Assert.True(artifacts.Success);
            Assert.NotNull(artifacts.Metrics);

            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Eval.Map1, out var map));
            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Eval.Ndcg3, out var ndcg));

            // With the current sample texts we expect near-perfect ranking,
            // but we keep a small tolerance window.
            Assert.InRange(map, 0.9, 1.01);
            Assert.InRange(ndcg, 0.9, 1.01);

            // Persist the run result in a dedicated test folder.
            var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "MiniInsuranceRuns");
            var runDir = await RunPersistor.Persist(baseDir, artifacts);

            // Verify that the run directory and at least one markdown file exist.
            Assert.True(Directory.Exists(runDir));

            var mdFiles = Directory.GetFiles(runDir, "*.md", SearchOption.AllDirectories);
            Assert.NotEmpty(mdFiles);
        }
    }
}
