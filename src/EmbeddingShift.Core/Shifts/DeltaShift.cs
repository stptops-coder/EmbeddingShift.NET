using System;
using EmbeddingShift.Abstractions.Shifts;

namespace EmbeddingShift.Core.Shifts
{
    /// <summary>
    /// v1 implementation of a "delta" shift.
    /// This represents an adjustment on top of the base (first) shift,
    /// e.g. derived from feedback or a specific policy/corpus slice.
    /// </summary>
    public sealed class DeltaShift : IEmbeddingShift
    {
        private readonly float[] _deltaVector;

        public DeltaShift(
            string name,
            float[] deltaVector,
            float weight = 1.0f,
            string? source = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _deltaVector = deltaVector ?? throw new ArgumentNullException(nameof(deltaVector));
            Weight = weight;
            Source = source;
        }

        public string Name { get; }

        public ShiftStage Stage => ShiftStage.Delta;

        public float Weight { get; }

        /// <summary>
        /// Optional free-form description that can be used to identify
        /// where this delta shift comes from (e.g. a feedback run id).
        /// </summary>
        public string? Source { get; }

        public void ApplyInPlace(float[] embedding)
        {
            if (embedding == null)
            {
                throw new ArgumentNullException(nameof(embedding));
            }

            if (embedding.Length != _deltaVector.Length)
            {
                throw new ArgumentException(
                    "Embedding and delta vector must have the same length.",
                    nameof(embedding));
            }

            var factor = Weight;
            if (Math.Abs(factor) < float.Epsilon)
            {
                return;
            }

            for (var i = 0; i < embedding.Length; i++)
            {
                embedding[i] += factor * _deltaVector[i];
            }
        }
    }
}
