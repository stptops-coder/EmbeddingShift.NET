using EmbeddingShift.Abstractions;
using EmbeddingShift.Core;
using Xunit;

namespace EmbeddingShift.Tests;

public class AdditiveShiftTests
{
    [Fact]
    public void AddsBiasElementwise()
    {
        var x = new float[EmbeddingDimensions.DIM];
        var b = new float[EmbeddingDimensions.DIM];

        // Probe: setze zwei Stellen, Rest 0
        x[0] = 1f; x[10] = 2f;
        b[0] = 0.5f; b[10] = -1f;

        var shift = new AdditiveShift(b);
        var y = shift.Apply(x);

        Assert.Equal(1f + 0.5f, y[0], 5);
        Assert.Equal(2f - 1f,  y[10], 5);
        Assert.Equal(EmbeddingDimensions.DIM, y.Length);
    }
}

