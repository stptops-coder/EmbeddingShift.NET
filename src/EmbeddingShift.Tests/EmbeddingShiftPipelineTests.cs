using System.Collections.Generic;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.Core.Shifts;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies the basic behavior of EmbeddingShiftPipeline:
    /// - ordering by stage (First -> Delta) and then by name
    /// - no modification when the pipeline is empty.
    /// </summary>
    public class EmbeddingShiftPipelineTests
    {
        private sealed class TestShift : IEmbeddingShift
        {
            private readonly List<string> _log;

            public TestShift(List<string> log, string name, ShiftStage stage)
            {
                _log = log;
                Name = name;
                Stage = stage;
            }

            public string Name { get; }

            public ShiftStage Stage { get; }

            public float Weight => 1.0f;

            public void ApplyInPlace(float[] embedding)
            {
                _log.Add($"{Stage}:{Name}");
            }
        }

        [Fact]
        public void Pipeline_orders_shifts_by_stage_then_name()
        {
            var log = new List<string>();

            var shifts = new IEmbeddingShift[]
            {
                new TestShift(log, "z", ShiftStage.Delta),
                new TestShift(log, "a", ShiftStage.Delta),
                new TestShift(log, "b", ShiftStage.First),
                new TestShift(log, "a", ShiftStage.First)
            };

            var pipeline = new EmbeddingShiftPipeline(shifts);

            var embedding = new float[1];

            pipeline.ApplyInPlace(embedding);

            Assert.Equal(
                new[]
                {
                    "First:a",
                    "First:b",
                    "Delta:a",
                    "Delta:z"
                },
                log);
        }

        [Fact]
        public void Pipeline_does_not_modify_embedding_when_empty()
        {
            var embedding = new float[] { 1f, 2f, 3f };

            var pipeline = new EmbeddingShiftPipeline(System.Array.Empty<IEmbeddingShift>());

            pipeline.ApplyInPlace(embedding);

            Assert.Equal(new[] { 1f, 2f, 3f }, embedding);
        }
    }
}
