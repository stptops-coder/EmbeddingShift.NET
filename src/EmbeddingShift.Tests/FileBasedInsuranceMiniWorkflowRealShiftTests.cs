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
    /// Runs the file-based insurance mini workflow with a non-trivial
    /// shift pipeline that actually modifies the embeddings.
    /// This is our first "real" end-to-end B-Light style experiment,
    /// but only in the test context (no change to the default pipeline).
    /// </summary>
    public class FileBasedInsuranceMiniWorkflowRealShiftTests
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
        public async Task File_based_insurance_workflow_runs_successfully_with_real_shift_pipeline()
        {
            // This pipeline will add a constant bias to every embedding dimension.
            // We do not assert any metric here – only that the workflow remains stable.
            var shiftPipeline = new IncrementShiftPipeline(delta: 1.0f);

            IWorkflow workflow = new FileBasedInsuranceMiniWorkflow(shiftPipeline);
            var runner         = new StatsAwareWorkflowRunner();

            var artifacts = await runner.ExecuteAsync(
                "FileBased-Insurance-Mini-RealShift",
                workflow);

            Assert.True(artifacts.Success);
        }
    }
}
