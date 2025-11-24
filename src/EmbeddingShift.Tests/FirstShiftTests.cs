using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.Core.Shifts;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies the concrete behavior of FirstShift
    /// (our first "real" shift) and its usage in the pipeline.
    /// </summary>
    public class FirstShiftTests
    {
        [Fact]
        public void FirstShift_applies_weighted_shift_vector_in_place()
        {
            var shiftVector = new float[] { 10f, 20f, 30f };
            var embedding   = new float[] {  1f,  2f,  3f };

            var shift = new FirstShift("test-first", shiftVector, weight: 1.0f);

            shift.ApplyInPlace(embedding);

            Assert.Equal(new[] { 11f, 22f, 33f }, embedding);
            Assert.Equal("test-first", shift.Name);
            Assert.Equal(ShiftStage.First, shift.Stage);
            Assert.Equal(1.0f, shift.Weight);
        }

        [Fact]
        public void FirstShift_can_be_used_in_pipeline()
        {
            var shiftVector = new float[] { 1f, 2f };
            var embedding   = new float[] { 0f, 0f };

            IEmbeddingShift firstShift = new FirstShift("pipeline-first", shiftVector, 1.0f);
            var pipeline = new EmbeddingShiftPipeline(new[] { firstShift });

            pipeline.ApplyInPlace(embedding);

            Assert.Equal(new[] { 1f, 2f }, embedding);
        }
    }
}
