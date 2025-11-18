using System.Threading.Tasks;
using EmbeddingShift.Core.Stats;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that the file-based insurance mini workflow
    /// can read the sample files and produces reasonable metrics.
    /// </summary>
    public class FileBasedInsuranceMiniWorkflowTests
    {
        [Fact]
        public async Task File_based_insurance_workflow_produces_metrics_from_sample_files()
        {
            IWorkflow workflow = new FileBasedInsuranceMiniWorkflow();
            var runner         = new StatsAwareWorkflowRunner();

            var artifacts = await runner.ExecuteAsync("FileBased-Insurance-Mini", workflow);

            Assert.True(artifacts.Success);
            Assert.NotNull(artifacts.Metrics);

            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Eval.Map1,  out var map));
            Assert.True(artifacts.Metrics.TryGetValue(MetricKeys.Eval.Ndcg3, out var ndcg));

            // With the current sample texts we expect near-perfect ranking,
            // but we keep a small tolerance window.
            Assert.InRange(map,  0.9, 1.01);
            Assert.InRange(ndcg, 0.9, 1.01);
        }
    }
}
