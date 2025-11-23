using System;

namespace EmbeddingShift.Abstractions.Shifts
{
    /// <summary>
    /// Stage in which a shift is applied.
    /// First  = base/domain shift (v1).
    /// Delta  = adjustments on top of the base shift (v1).
    /// </summary>
    public enum ShiftStage
    {
        First,
        Delta
    }

    /// <summary>
    /// Describes a single embedding shift that can be applied in-place.
    /// v1: simple additive shift on a float[] embedding.
    /// </summary>
    public interface IEmbeddingShift
    {
        /// <summary>
        /// Logical name of the shift (e.g. "insurance-first", "policy-123-delta").
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Stage of the shift in the pipeline (First vs Delta).
        /// </summary>
        ShiftStage Stage { get; }

        /// <summary>
        /// Global weight for this shift (1.0 = full effect).
        /// </summary>
        float Weight { get; }

        /// <summary>
        /// Applies the shift in-place to the given embedding.
        /// </summary>
        /// <param name="embedding">
        /// Embedding vector that will be modified in-place.
        /// </param>
        void ApplyInPlace(float[] embedding);
    }
}
