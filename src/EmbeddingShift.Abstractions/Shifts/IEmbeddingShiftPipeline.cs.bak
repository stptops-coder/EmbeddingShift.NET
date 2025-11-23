using System.Collections.Generic;

namespace EmbeddingShift.Abstractions.Shifts
{
    /// <summary>
    /// Deterministic sequence of embedding shifts.
    /// v1: operates on float[] in-place and orders shifts by stage, then by name.
    /// </summary>
    public interface IEmbeddingShiftPipeline
    {
        /// <summary>
        /// Shifts that are part of the pipeline, in the order they are applied.
        /// </summary>
        IReadOnlyList<IEmbeddingShift> Shifts { get; }

        /// <summary>
        /// Applies all shifts to the provided embedding in-place.
        /// </summary>
        /// <param name="embedding">
        /// Embedding vector that will be modified in-place.
        /// </param>
        void ApplyInPlace(float[] embedding);
    }
}
