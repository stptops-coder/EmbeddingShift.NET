using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Shifts;

public sealed class NoShiftIngestBased : IShift
{
    public string Name => "NoShift.IngestBased";
    public ShiftKind Kind => ShiftKind.NoShift;

    public ReadOnlyMemory<float> Apply(ReadOnlySpan<float> input)
    {
        // Identity: returns a copy to satisfy ReadOnlyMemory<float> return without borrowing.
        var clone = new float[input.Length];
        input.CopyTo(clone);
        return clone;
    }
}
