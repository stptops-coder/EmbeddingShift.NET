using System.Collections.Generic;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Uses the FirstShift / DeltaShift pipeline helpers of the file-based
    /// insurance mini workflow and asserts that metrics do not degrade
    /// compared to the baseline run.
    ///
    /// This stays purely in the test domain: the default pipeline of the
    /// workflow remains a no-op.
    /// </summary>
    public class FileBasedInsuranceMiniWorkflowFirstDeltaShiftTests
    {
        [Fact]
        public async Task First_and_first_plus_delta_do_not_degrade_metrics()
        {
            var runner = new StatsAwareWorkflowRunner();

            // Baseline: default workflow with no-op pipeline.
            IWorkflow baselineWorkflow = new FileBasedInsuranceMiniWorkflow();
            var baselineResult = await runner.ExecuteAsync(
                "FileBased-Insurance-Mini-Baseline-FirstDelta",
                baselineWorkflow);

            Assert.True(baselineResult.Success);

            // FirstShift only.
            IEmbeddingShiftPipeline firstPipeline = FileBasedInsuranceMiniWorkflow.CreateFirstShiftPipeline();
            IWorkflow firstWorkflow = new FileBasedInsuranceMiniWorkflow(firstPipeline);

            var firstResult = await runner.ExecuteAsync(
                "FileBased-Insurance-Mini-FirstShift",
                firstWorkflow);

            Assert.True(firstResult.Success);

            // First + DeltaShift.
            IEmbeddingShiftPipeline firstPlusDeltaPipeline = FileBasedInsuranceMiniWorkflow.CreateFirstPlusDeltaPipeline();
            IWorkflow firstPlusDeltaWorkflow = new FileBasedInsuranceMiniWorkflow(firstPlusDeltaPipeline);

            var firstPlusDeltaResult = await runner.ExecuteAsync(
                "FileBased-Insurance-Mini-FirstPlusDelta",
                firstPlusDeltaWorkflow);

            Assert.True(firstPlusDeltaResult.Success);

            var baselineMetrics = baselineResult.Metrics ?? new Dictionary<string, double>();
            var firstMetrics = firstResult.Metrics ?? new Dictionary<string, double>();
            var firstPlusDeltaMetrics = firstPlusDeltaResult.Metrics ?? new Dictionary<string, double>();

            var allKeys = new SortedSet<string>(baselineMetrics.Keys);
            allKeys.UnionWith(firstMetrics.Keys);
            allKeys.UnionWith(firstPlusDeltaMetrics.Keys);

            const double Tolerance = 1e-6;

            foreach (var key in allKeys)
            {
                baselineMetrics.TryGetValue(key, out var b);
                firstMetrics.TryGetValue(key, out var f);
                firstPlusDeltaMetrics.TryGetValue(key, out var fd);

                Assert.True(
                    f + Tolerance >= b,
                    $"Metric '{key}' degraded with FirstShift: baseline={b}, first={f}");

                Assert.True(
                    fd + Tolerance >= b,
                    $"Metric '{key}' degraded with First+Delta: baseline={b}, firstPlusDelta={fd}");
            }
        }
    }
}
