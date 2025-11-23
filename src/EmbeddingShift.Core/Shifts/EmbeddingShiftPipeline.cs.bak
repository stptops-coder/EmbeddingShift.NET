using System;
using System.Collections.Generic;
using System.Linq;
using EmbeddingShift.Abstractions.Shifts;

namespace EmbeddingShift.Core.Shifts
{
    /// <summary>
    /// v1 implementation of an embedding shift pipeline.
    /// Shifts are ordered by stage (First -> Delta) and then by name,
    /// and applied in that deterministic order.
    /// </summary>
    public sealed class EmbeddingShiftPipeline : IEmbeddingShiftPipeline
    {
        private readonly IReadOnlyList<IEmbeddingShift> _shifts;

        public EmbeddingShiftPipeline(IEnumerable<IEmbeddingShift> shifts)
        {
            if (shifts == null)
            {
                throw new ArgumentNullException(nameof(shifts));
            }

            _shifts = shifts
                .OrderBy(s => s.Stage)   // ShiftStage enum: First, Delta
                .ThenBy(s => s.Name)
                .ToArray();

            Shifts = _shifts;
        }

        /// <summary>
        /// Shifts in the order they are applied.
        /// </summary>
        public IReadOnlyList<IEmbeddingShift> Shifts { get; }

        /// <summary>
        /// Applies all shifts to the given embedding in-place.
        /// </summary>
        /// <param name="embedding">
        /// Embedding vector that will be modified in-place.
        /// </param>
        public void ApplyInPlace(float[] embedding)
        {
            if (embedding == null)
            {
                throw new ArgumentNullException(nameof(embedding));
            }

            if (_shifts.Count == 0)
            {
                return;
            }

            foreach (var shift in _shifts)
            {
                shift.ApplyInPlace(embedding);
            }
        }
    }
}
