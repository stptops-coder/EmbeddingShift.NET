namespace EmbeddingShift.Abstractions
{
    /// <summary>
    /// Global switch for baseline vs. shifted evaluation modes.
    /// </summary>
    public enum ShiftMethod
    {
        /// <summary>
        /// Baseline: evaluates embeddings as-is (NoShiftIngestBased).
        /// </summary>
        NoShiftIngestBased = 0,


        /// <summary>
        /// Shifted: applies additive, multiplicative, or adaptive transformations.
        /// </summary>
        Shifted = 1
    }
}
