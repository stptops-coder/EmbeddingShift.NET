using EmbeddingShift.Core.Shifts;
using System.Runtime.InteropServices;

using Xunit;

namespace EmbeddingShift.Tests
{
    public sealed class NoShiftIngestBasedTests
    {
        [Fact]
        public void Returns_Input_Unchanged()
        {
            var input = new float[] { 0.1f, -2f, 3.5f, 0f };
            var s = new NoShiftIngestBased();
            var output = s.Apply(input);
            Assert.Equal(input, output);
        }

        [Fact]
        public void Apply_Returns_Copy_NotSameReference()
        {
            var input = new float[] { 0.1f, -2f, 3.5f, 0f };
            var s = new NoShiftIngestBased();

            var output = s.Apply(input);

            // Access the underlying array of the ReadOnlyMemory output:
            Assert.True(MemoryMarshal.TryGetArray(output, out ArraySegment<float> seg));

            // References must NOT be identical (must be a copy)
            Assert.NotSame(input, seg.Array!);
        }
        [Fact]
        public void Changing_Input_After_Apply_Does_Not_Affect_Output()
        {
            var input = new float[] { 1f, 2f, 3f };
            var s = new NoShiftIngestBased();

            var outputBefore = s.Apply(input).ToArray(); // Materialize BEFORE mutation
            input[0] = 99f; // mutate input after Apply

            // Output remains unchanged
            Assert.Equal(new float[] { 1f, 2f, 3f }, outputBefore);
        }

    }
}
