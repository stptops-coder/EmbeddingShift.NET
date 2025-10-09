using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Shifts;

/// <summary>
/// Identity shift bound to the ingest baseline.
/// Returns a copy of the input so that callers receive a stable ReadOnlyMemory.
/// </summary>
public sealed class NoShiftIngestBased : IShift
{
    public string Name => "NoShift.IngestBased";
    public ShiftKind Kind => ShiftKind.NoShift;

    public ReadOnlyMemory<float> Apply(ReadOnlySpan<float> input)
    {
        var copy = new float[input.Length];
        input.CopyTo(copy);
        return new ReadOnlyMemory<float>(copy);
    }
}
