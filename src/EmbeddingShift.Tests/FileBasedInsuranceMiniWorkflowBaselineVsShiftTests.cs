using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.Core.Stats;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Runs the file-based insurance mini workflow twice:
    /// - once without any shift pipeline (baseline)
    /// - once with a real shift pipeline that modifies embeddings
    ///
    /// In addition to checking Success, this test prepares a
    /// Baseline-vs-Shift metric comparison based on WorkflowResult.Metrics.
    /// There are intentionally no hard metric gates yet.
    /// </summary>
    public class FileBasedInsuranceMiniWorkflowBaselineVsShiftTests
    {
        private sealed class IncrementShiftPipeline : IEmbeddingShiftPipeline
        {
            private readonly float _delta;

            public IncrementShiftPipeline(float delta)
            {
                _delta = delta;
                Shifts = Array.Empty<IEmbeddingShift>();
            }

            public IReadOnlyList<IEmbeddingShift> Shifts { get; }

            public void ApplyInPlace(float[] embedding)
            {
                if (embedding == null)
                {
                    throw new ArgumentNullException(nameof(embedding));
                }

                for (var i = 0; i < embedding.Length; i++)
                {
                    embedding[i] += _delta;
                }
            }
        }

        [Fact]
        public async Task Baseline_and_shifted_runs_complete_successfully_and_expose_metrics_for_comparison()
        {
            var runner = new StatsAwareWorkflowRunner();

            // Baseline: default workflow with no-op pipeline
            IWorkflow baselineWorkflow = new FileBasedInsuranceMiniWorkflow();
            WorkflowResult baselineResult = await runner.ExecuteAsync(
                "FileBased-Insurance-Mini-Baseline",
                baselineWorkflow);

            Assert.True(baselineResult.Success);

            // Shifted: same workflow, but with a real shift pipeline
            var shiftPipeline = new IncrementShiftPipeline(delta: 0.5f);
            IWorkflow shiftedWorkflow = new FileBasedInsuranceMiniWorkflow(shiftPipeline);

            WorkflowResult shiftedResult = await runner.ExecuteAsync(
                "FileBased-Insurance-Mini-Shifted",
                shiftedWorkflow);

            Assert.True(shiftedResult.Success);

            // --- Metric comparison scaffold ------------------------------------
            // WorkflowResult exposes Metrics as IReadOnlyDictionary<string,double>.
            // We build a combined view over baseline and shifted metrics.
            var baselineMetrics = baselineResult.Metrics ?? new Dictionary<string, double>();
            var shiftedMetrics  = shiftedResult.Metrics  ?? new Dictionary<string, double>();

            var allKeys = new SortedSet<string>(baselineMetrics.Keys);
            allKeys.UnionWith(shiftedMetrics.Keys);

            var metricDiffs = new Dictionary<string, (double Baseline, double Shifted, double Delta)>();

            foreach (var key in allKeys)
            {
                baselineMetrics.TryGetValue(key, out var b);
                shiftedMetrics.TryGetValue(key, out var s);
                metricDiffs[key] = (b, s, s - b);
            }

            // This assertion is just to keep the test from being "empty".
            // To inspect concrete values, set a breakpoint on metricDiffs.
            Assert.NotNull(metricDiffs);
        }
    }
}
