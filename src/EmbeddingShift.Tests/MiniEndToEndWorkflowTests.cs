using System.Threading.Tasks;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that MiniSemanticRetrievalWorkflow (Workflows layer)
    /// produces the expected metrics when executed via StatsAwareWorkflowRunner.
    /// </summary>
    public class MiniEndToEndWorkflowTests
    {
        [Fact]
        public async Task Mini_semantic_retrieval_workflow_produces_expected_metrics()
        {
            IWorkflow workflow = new MiniSemanticRetrievalWorkflow();
            var runner   = new StatsAwareWorkflowRunner();

            var artifacts = await runner.ExecuteAsync("Mini-EndToEnd", workflow);

            Assert.True(artifacts.Success);
            Assert.NotNull(artifacts.Metrics);

            Assert.True(artifacts.Metrics.TryGetValue("map@1", out var map));
            Assert.True(artifacts.Metrics.TryGetValue("ndcg@3", out var ndcg));

            Assert.InRange(map,  0.99, 1.01);
            Assert.InRange(ndcg, 0.99, 1.01);
        }
    }
}
