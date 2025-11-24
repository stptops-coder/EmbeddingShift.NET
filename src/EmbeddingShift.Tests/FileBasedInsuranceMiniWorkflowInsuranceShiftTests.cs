using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.Core.Shifts;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Uses the InsuranceKeywordBoostShift in the file-based insurance mini workflow
    /// and asserts that the metrics do not degrade compared to the baseline run.
    /// This stays in the test domain: the default pipeline of the workflow
    /// remains a no-op.
    /// </summary>
    public class FileBasedInsuranceMiniWorkflowInsuranceShiftTests
    {
        [Fact]
        public async Task Insurance_keyword_boost_shift_does_not_degrade_metrics()
        {
            var runner = new StatsAwareWorkflowRunner();

            // Baseline: default no-op pipeline
            IWorkflow baselineWorkflow = new FileBasedInsuranceMiniWorkflow();
            var baselineResult = await runner.ExecuteAsync(
                "FileBased-Insurance-Mini-Baseline-InsuranceShift",
                baselineWorkflow);

            Assert.True(baselineResult.Success);

            // Shifted: pipeline with InsuranceKeywordBoostShift
            IEmbeddingShift boostShift = new InsuranceKeywordBoostShift(
                damageBoost: 0.5f,
                claimsBoost: 0.5f,
                floodBoost:  0.5f);

            var pipeline = new EmbeddingShiftPipeline(new[] { boostShift });

            IWorkflow shiftedWorkflow = new FileBasedInsuranceMiniWorkflow(pipeline);
            var shiftedResult = await runner.ExecuteAsync(
                "FileBased-Insurance-Mini-InsuranceShift",
                shiftedWorkflow);

            Assert.True(shiftedResult.Success);

            var baselineMetrics = baselineResult.Metrics ?? new Dictionary<string, double>();
            var shiftedMetrics  = shiftedResult.Metrics  ?? new Dictionary<string, double>();

            var allKeys = new SortedSet<string>(baselineMetrics.Keys);
            allKeys.UnionWith(shiftedMetrics.Keys);

            const double Tolerance = 1e-6;

            foreach (var key in allKeys)
            {
                baselineMetrics.TryGetValue(key, out var b);
                shiftedMetrics.TryGetValue(key, out var s);
                var delta = s - b;

                // Soft gate: the insurance shift must not make metrics worse.
                Assert.True(
                    s + Tolerance >= b,
                    $"Metric '{key}' degraded with InsuranceKeywordBoostShift: baseline={b}, shifted={s}, delta={delta}");
            }
        }
    }
}
