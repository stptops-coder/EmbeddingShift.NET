using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Shifts
{
    /// <summary>
    /// Identity shift for "Method A" (ingest-based, no model-side shift).
    /// Returns the input unchanged.
    /// </summary>
    public sealed class NoShiftIngestBased : IShift
    {
        public string Name => "NoShiftIngestBased";

        public ReadOnlyMemory<float> Apply(ReadOnlySpan<float> input)
        {
            var r = new float[input.Length];
            input.CopyTo(r);
            return r;
        }

        public override string ToString() => "NoShiftIngestBased (identity)";
    }
}
