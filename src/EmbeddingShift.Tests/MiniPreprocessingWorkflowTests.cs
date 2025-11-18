using System.Threading.Tasks;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

using EmbeddingShift.Core.Stats;
namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that MiniPreprocessingWorkflow runs successfully
    /// and produces reasonable preprocessing metrics.
    /// </summary>
    public class MiniPreprocessingWorkflowTests
    {
        [Fact]
        public async Task Mini_preprocessing_workflow_produces_basic_metrics()
        {
            IWorkflow workflow = new MiniPreprocessingWorkflow();
            var runner         = new StatsAwareWorkflowRunner();

            var artifacts = await runner.ExecuteAsync("Mini-Preprocessing", workflow);

            Assert.True(artifacts.Success);
            Assert.NotNull(artifacts.Metrics);

            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Prep.TotalDocs, out var totalDocs));
            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Prep.TotalChunks, out var totalChunks));
            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Prep.AvgChunkLength, out var avgChunkLength));
            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Prep.AvgWhitespace, out var avgWs));

            Assert.True(totalDocs >= 3);
            Assert.True(totalChunks >= totalDocs);
            Assert.True(avgChunkLength > 0);
            Assert.InRange(avgWs, 0.0, 1.0);
        }
    }
}

