using System.Threading.Tasks;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

using EmbeddingShift.Core.Stats;
namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that MiniIngestWorkflow runs successfully
    /// and produces basic ingest metrics.
    /// </summary>
    public class MiniIngestWorkflowTests
    {
        [Fact]
        public async Task Mini_ingest_workflow_produces_basic_metrics()
        {
            IWorkflow workflow = new MiniIngestWorkflow();
            var runner         = new StatsAwareWorkflowRunner();

            var artifacts = await runner.ExecuteAsync("Mini-Ingest", workflow);

            Assert.True(artifacts.Success);
            Assert.NotNull(artifacts.Metrics);

            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Ingest.TotalDocs, out var totalDocs));
            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Ingest.TotalChunks, out var totalChunks));
            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Ingest.EmbeddingDim, out var embDim));
            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Ingest.AvgEmbeddingNorm, out var avgNorm));

            Assert.True(totalDocs >= 3);
            Assert.True(totalChunks >= totalDocs);
            Assert.True(embDim > 0);
            Assert.True(avgNorm > 0);
        }
    }
}

