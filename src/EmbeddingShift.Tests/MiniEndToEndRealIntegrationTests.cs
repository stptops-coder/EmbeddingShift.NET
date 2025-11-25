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
    /// Full end-to-end integration test for the mini semantic retrieval workflow.
    /// It runs the workflow via StatsAwareWorkflowRunner (using the SIM embedding
    /// provider pipeline), validates core metrics and persists the result via
    /// RunPersistor. This combines the existing MiniEndToEndWorkflowTests and
    /// RunPersistorSmokeTests into a single "real" system integration check.
    /// </summary>
    public class MiniEndToEndRealIntegrationTests
    {
        [Fact]
        public async Task Mini_end_to_end_semantic_workflow_can_be_persisted_with_valid_metrics()
        {
            // Arrange: mini semantic retrieval workflow using the SIM provider
            // and toy documents/queries.
            IWorkflow workflow = new MiniSemanticRetrievalWorkflow();
            var runner = new StatsAwareWorkflowRunner();

            // Act: execute workflow end-to-end.
            var artifacts = await runner.ExecuteAsync("Mini-EndToEnd-Persisted", workflow);

            // Assert: workflow succeeded and produced metrics.
            Assert.True(artifacts.Success);
            Assert.NotNull(artifacts.Metrics);

            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Eval.Map1, out var map));
            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Eval.Ndcg3, out var ndcg));

            // With the current toy data we expect near-perfect ranking,
            // but we keep a small tolerance window.
            Assert.InRange(map, 0.9, 1.01);
            Assert.InRange(ndcg, 0.9, 1.01);

            // Persist the run result in a dedicated test folder.
            var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "MiniEndToEndRuns");
            var runDir = await RunPersistor.Persist(baseDir, artifacts);

            // Verify that the run directory and at least one markdown file exist.
            Assert.True(Directory.Exists(runDir));

            var mdFiles = Directory.GetFiles(runDir, "*.md", SearchOption.AllDirectories);
            Assert.NotEmpty(mdFiles);
        }
    }
}
