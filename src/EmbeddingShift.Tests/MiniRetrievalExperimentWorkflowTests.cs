using System.Threading.Tasks;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that MiniRetrievalExperimentWorkflow runs successfully
    /// and produces baseline and variant metrics.
    /// </summary>
    public class MiniRetrievalExperimentWorkflowTests
    {
        [Fact]
        public async Task Mini_retrieval_experiment_produces_baseline_and_variant_metrics()
        {
            IWorkflow workflow = new MiniRetrievalExperimentWorkflow();
            var runner         = new StatsAwareWorkflowRunner();

            var artifacts = await runner.ExecuteAsync("Mini-Retrieval-Experiment", workflow);

            Assert.True(artifacts.Success);
            Assert.NotNull(artifacts.Metrics);

            Assert.True(artifacts.Metrics.TryGetValue("eval.map@1.baseline",   out var mapBaseline));
            Assert.True(artifacts.Metrics.TryGetValue("eval.ndcg@3.baseline", out var ndcgBaseline));
            Assert.True(artifacts.Metrics.TryGetValue("eval.map@1.variant",    out var mapVariant));
            Assert.True(artifacts.Metrics.TryGetValue("eval.ndcg@3.variant",  out var ndcgVariant));
            Assert.True(artifacts.Metrics.TryGetValue("eval.delta.map@1",      out var deltaMap));
            Assert.True(artifacts.Metrics.TryGetValue("eval.delta.ndcg@3",    out var deltaNdcg));

            Assert.InRange(mapBaseline,  0.0, 1.0);
            Assert.InRange(ndcgBaseline, 0.0, 1.0);
            Assert.InRange(mapVariant,   0.0, 1.0);
            Assert.InRange(ndcgVariant,  0.0, 1.0);

            // deltas can be positive, zero or negative; here we only check that they are finite numbers
            Assert.False(double.IsNaN(deltaMap));
            Assert.False(double.IsNaN(deltaNdcg));
        }
    }
}
