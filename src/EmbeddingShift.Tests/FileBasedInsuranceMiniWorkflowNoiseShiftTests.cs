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
    /// Runs the file-based insurance mini workflow twice:
    /// - baseline: default no-op pipeline
    /// - shifted: InsuranceKeywordBoostShift + RandomNoiseShift
    ///
    /// The goal of this test is to verify that the workflow can be executed
    /// end-to-end with a combination of a semantic shift and a stochastic
    /// noise shift. We only assert basic metric sanity here; detailed
    /// non-degradation gates are handled in separate tests.
    /// </summary>
    public class FileBasedInsuranceMiniWorkflowNoiseShiftTests
    {
        [Fact]
        public async Task Insurance_boost_plus_noise_runs_successfully_and_exposes_sane_metrics()
        {
            var runner = new StatsAwareWorkflowRunner();

            // Baseline run: no shift pipeline
            IWorkflow baselineWorkflow = new FileBasedInsuranceMiniWorkflow();
            var baselineResult = await runner.ExecuteAsync(
                "FileBased-Insurance-Mini-Baseline-Noise",
                baselineWorkflow);

            Assert.True(baselineResult.Success);

            // Shifted run: InsuranceKeywordBoostShift + RandomNoiseShift
            IEmbeddingShift insuranceShift = new InsuranceKeywordBoostShift(
                damageBoost: 0.5f,
                claimsBoost: 0.5f,
                floodBoost:  0.5f);

            // Deterministic noise via seeded RNG.
            var rng = new Random(2025);
            IEmbeddingShift noiseShift = new RandomNoiseShift(
                noiseAmplitude: 0.05f,
                rng: rng);

            var pipeline = new EmbeddingShiftPipeline(new IEmbeddingShift[]
            {
                insuranceShift,
                noiseShift
            });

            IWorkflow shiftedWorkflow = new FileBasedInsuranceMiniWorkflow(pipeline);
            var shiftedResult = await runner.ExecuteAsync(
                "FileBased-Insurance-Mini-InsuranceShift-WithNoise",
                shiftedWorkflow);

            Assert.True(shiftedResult.Success);

            var baselineMetrics = baselineResult.Metrics ?? new Dictionary<string, double>();
            var shiftedMetrics  = shiftedResult.Metrics  ?? new Dictionary<string, double>();

            Assert.NotEmpty(baselineMetrics);
            Assert.NotEmpty(shiftedMetrics);

            // We expect the same set of metric keys (e.g. map@1, ndcg@3).
            var baselineKeys = new SortedSet<string>(baselineMetrics.Keys);
            var shiftedKeys  = new SortedSet<string>(shiftedMetrics.Keys);

            Assert.Equal(baselineKeys, shiftedKeys);

            // Basic sanity: all metric values are in [0, 1], both baseline and shifted.
            foreach (var key in baselineKeys)
            {
                baselineMetrics.TryGetValue(key, out var b);
                shiftedMetrics.TryGetValue(key, out var s);

                Assert.InRange(b, 0.0, 1.0);
                Assert.InRange(s, 0.0, 1.0);
            }
        }
    }
}
