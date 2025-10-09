using EmbeddingShift.Core.Shifts;
using FluentAssertions;
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
            output.Span.ToArray().Should().BeEquivalentTo(input);
        }

        [Fact]
        public void Apply_Returns_Copy_NotSameReference()
        {
            var input = new float[] { 0.1f, -2f, 3.5f, 0f };
            var s = new NoShiftIngestBased();

            var output = s.Apply(input);

            // Zugriff auf das zugrundeliegende Array der ReadOnlyMemory-Ausgabe:
            Assert.True(MemoryMarshal.TryGetArray(output, out ArraySegment<float> seg));

            // Referenzen dürfen NICHT identisch sein (muss Kopie sein)
            Assert.NotSame(input, seg.Array);

            // Werte müssen identisch sein
            seg.Array!.Should().BeEquivalentTo(input);
        }
        [Fact]
        public void Changing_Input_After_Apply_Does_Not_Affect_Output()
        {
            var input = new float[] { 1f, 2f, 3f };
            var s = new NoShiftIngestBased();

            var outputBefore = s.Apply(input).ToArray(); // Materialize BEFORE mutation
            input[0] = 99f; // mutate input after Apply

            // Ausgabe bleibt unverändert
            outputBefore.Should().BeEquivalentTo(new float[] { 1f, 2f, 3f });
        }

    }
}
