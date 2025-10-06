using EmbeddingShift.Core.Shifts;
using FluentAssertions;
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
            output.Span.ToArray().Should().BeEquivalentTo(input);
        }
    }
}
