using System;
using System.Collections.Generic;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.Core.Shifts;

namespace EmbeddingShift.Adaptive
{
    /// <summary>
    /// IShiftGenerator that turns the latest persisted ShiftTrainingResult
    /// for a given workflow into a learned additive shift.
    ///
    /// This is the bridge between the "statistics / training" layer
    /// (ShiftTrainingResultRepository) and the adaptive layer.
    /// </summary>
    public sealed class TrainingBackedShiftGenerator : IShiftGenerator
    {
        private readonly IShiftTrainingResultRepository _repository;
        private readonly string _workflowName;
        private readonly IShift _fallbackShift;

        /// <summary>
        /// Creates a new generator that reads the latest training result
        /// for the specified workflow name.
        /// </summary>
        /// <param name="repository">
        /// Repository that knows how to load ShiftTrainingResult (e.g. file system, DB).
        /// </param>
        /// <param name="workflowName">
        /// Logical workflow identifier, e.g. "mini-insurance-first-delta".
        /// </param>
        /// <param name="fallbackShift">
        /// Shift used when no training result (or no usable delta) is available.
        /// Default is NoShift.IngestBased.
        /// </param>
        public TrainingBackedShiftGenerator(
            IShiftTrainingResultRepository repository,
            string workflowName,
            IShift? fallbackShift = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));

            if (string.IsNullOrWhiteSpace(workflowName))
                throw new ArgumentException("Workflow name must not be null or whitespace.", nameof(workflowName));

            _workflowName = workflowName;
            _fallbackShift = fallbackShift ?? new NoShiftIngestBased();
        }

        /// <summary>
        /// Generates at most one learned shift from the latest training result.
        /// If no usable DeltaVector is present, falls back to the configured shift.
        ///
        /// NOTE: For now the candidate is global for the workflow and does not
        /// depend on the individual (Query, Answer) pairs.
        /// </summary>
        public IEnumerable<IShift> Generate(
            IReadOnlyList<(ReadOnlyMemory<float> Query, ReadOnlyMemory<float> Answer)> pairs)
        {
            var result = _repository.LoadLatest(_workflowName);

            // No training result or delta information: fall back to the base shift only.
            if (result == null || result.DeltaVector == null || result.DeltaVector.Length == 0)
            {
                yield return _fallbackShift;
                yield break;
            }

            var vector = NormalizeToEmbeddingDim(result.DeltaVector);
            if (IsZero(vector))
            {
                // Delta vector is effectively zero, there is no meaningful learned shift.
                yield return _fallbackShift;
                yield break;
            }

            // From here on we have a usable learned Delta vector.
            // Always include the fallback shift as a candidate so that the adaptive
            // layer can decide whether "no shift" is better than the learned shift.
            yield return _fallbackShift;
            yield return new AdditiveShift(vector);
        }

        private static float[] NormalizeToEmbeddingDim(float[] source)
        {
            if (source.Length == EmbeddingDimensions.DIM)
                return (float[])source.Clone();

            var normalized = new float[EmbeddingDimensions.DIM];
            var length = Math.Min(source.Length, normalized.Length);
            Array.Copy(source, normalized, length);
            return normalized;
        }

        private static bool IsZero(float[] vector)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                if (vector[i] != 0f)
                    return false;
            }

            return true;
        }
    }
}
