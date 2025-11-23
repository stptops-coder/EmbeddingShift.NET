using System;
using EmbeddingShift.Abstractions.Shifts;

namespace EmbeddingShift.Core.Shifts
{
    /// <summary>
    /// v1 implementation of a "first" (base) shift.
    /// This is typically the domain-level shift that is always applied.
    /// </summary>
    public sealed class FirstShift : IEmbeddingShift
    {
        private readonly float[] _shiftVector;

        public FirstShift(string name, float[] shiftVector, float weight = 1.0f)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _shiftVector = shiftVector ?? throw new ArgumentNullException(nameof(shiftVector));
            Weight = weight;
        }

        public string Name { get; }

        public ShiftStage Stage => ShiftStage.First;

        public float Weight { get; }

        public void ApplyInPlace(float[] embedding)
        {
            if (embedding == null)
            {
                throw new ArgumentNullException(nameof(embedding));
            }

            if (embedding.Length != _shiftVector.Length)
            {
                throw new ArgumentException(
                    "Embedding and shift vector must have the same length.",
                    nameof(embedding));
            }

            var factor = Weight;
            if (Math.Abs(factor) < float.Epsilon)
            {
                return;
            }

            for (var i = 0; i < embedding.Length; i++)
            {
                embedding[i] += factor * _shiftVector[i];
            }
        }
    }
}
