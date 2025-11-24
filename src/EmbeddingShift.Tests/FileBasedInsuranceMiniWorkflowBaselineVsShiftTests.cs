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
    /// This is a first Baseline-vs-Shift experiment without metric gates:
    /// we only assert that both runs complete successfully.
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
        public async Task Baseline_and_shifted_runs_complete_successfully()
        {
            var runner = new StatsAwareWorkflowRunner();

            // Baseline: default workflow with no-op pipeline
            IWorkflow baselineWorkflow = new FileBasedInsuranceMiniWorkflow();
            var baselineArtifacts = await runner.ExecuteAsync(
                "FileBased-Insurance-Mini-Baseline",
                baselineWorkflow);

            Assert.True(baselineArtifacts.Success);

            // Shifted: same workflow, but with a real shift pipeline
            var shiftPipeline = new IncrementShiftPipeline(delta: 0.5f);
            IWorkflow shiftedWorkflow = new FileBasedInsuranceMiniWorkflow(shiftPipeline);

            var shiftedArtifacts = await runner.ExecuteAsync(
                "FileBased-Insurance-Mini-Shifted",
                shiftedWorkflow);

            Assert.True(shiftedArtifacts.Success);
        }
    }
}
