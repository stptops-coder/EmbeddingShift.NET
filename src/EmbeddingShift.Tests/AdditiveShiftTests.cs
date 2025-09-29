using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Shifts;
using Xunit;

namespace EmbeddingShift.Tests;

public class AdditiveShiftTests
{
    [Fact]
    public void AddsBiasElementwise()
    {
        var x = new float[EmbeddingDimensions.DIM];
        var b = new float[EmbeddingDimensions.DIM];

        // sanity check: set two positions, the rest 0
        x[0] = 1f; x[10] = 2f;
        b[0] = 0.5f; b[10] = -1f;

        var shift = new AdditiveShift(b);
        var yMem = shift.Apply(x);           // ReadOnlyMemory<float>
        var y = yMem.Span;                // ReadOnlySpan<float> für Indexing

        Assert.Equal(1f + 0.5f, (double)y[0], 5);
        Assert.Equal(2f - 1f, (double)y[10], 5);
        Assert.Equal(EmbeddingDimensions.DIM, y.Length);
    }
}

