using System.Threading.Tasks;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

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

            Assert.True(artifacts.Metrics.TryGetValue("ingest.totalDocs", out var totalDocs));
            Assert.True(artifacts.Metrics.TryGetValue("ingest.totalChunks", out var totalChunks));
            Assert.True(artifacts.Metrics.TryGetValue("ingest.embeddingDim", out var embDim));
            Assert.True(artifacts.Metrics.TryGetValue("ingest.avgEmbeddingNorm", out var avgNorm));

            Assert.True(totalDocs >= 3);
            Assert.True(totalChunks >= totalDocs);
            Assert.True(embDim > 0);
            Assert.True(avgNorm > 0);
        }
    }
}
