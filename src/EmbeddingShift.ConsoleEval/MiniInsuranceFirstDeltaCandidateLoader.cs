using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Infrastructure;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.ConsoleEval.Repositories;
using EmbeddingShift.ConsoleEval.MiniInsurance;
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace EmbeddingShift.ConsoleEval
{
    /// <summary>
    /// Helper to load the latest trained Delta vector for the
    /// mini-insurance First/Delta setup.
    /// </summary>
    public static class MiniInsuranceFirstDeltaCandidateLoader
    {
        /// <summary>
        /// Loads the latest mini-insurance shift training result (candidate)
        /// from mini-insurance-first-delta-training_* directories under the
        /// given base directory. Returns null and found=false if no candidate
        /// could be loaded.
        /// </summary>
        public static MiniInsuranceShiftTrainingResult? LoadLatestCandidate(
            string baseDirectory,
            out bool found)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("Base directory must not be null or empty.", nameof(baseDirectory));

            var trainingDirs = Directory.GetDirectories(
                baseDirectory,
                "mini-insurance-first-delta-training_*",
                SearchOption.TopDirectoryOnly);

            if (trainingDirs.Length == 0)
            {
                found = false;
                return null;
            }

            Array.Sort(trainingDirs, StringComparer.Ordinal);
            Array.Reverse(trainingDirs);

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            foreach (var dir in trainingDirs)
            {
                var jsonPath = Path.Combine(dir, "shift-candidate.json");
                if (!File.Exists(jsonPath))
                    continue;

                try
                {
                    var json = File.ReadAllText(jsonPath, encoding);
                    var candidate = JsonSerializer.Deserialize<MiniInsuranceShiftTrainingResult>(json, jsonOptions);
                    if (candidate != null && candidate.DeltaVector != null)
                    {
                        found = true;
                        return candidate;
                    }
                }
                catch
                {
                    // ignore malformed candidates and continue
                }
            }

            found = false;
            return null;
        }

        /// <summary>
        /// Convenience overload using the standard insurance results root.
        /// </summary>
        public static MiniInsuranceShiftTrainingResult? LoadLatestCandidate(out bool found)
        {
            var baseDir = MiniInsurancePaths.GetDomainRoot();

            return LoadLatestCandidate(baseDir, out found);
        }

        public static float[] LoadLatestDeltaVectorOrDefault(
            string baseDirectory,
            out bool found)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("Base directory must not be null or empty.", nameof(baseDirectory));

            // 1) Try generic ShiftTrainingResultRepository first.
            try
            {
                var genericRepo = new FileSystemShiftTrainingResultRepository(baseDirectory);
                var generic = genericRepo.LoadLatest("mini-insurance-first-delta");

                if (generic != null && generic.DeltaVector != null && generic.DeltaVector.Length > 0)
                {
                    var source = generic.DeltaVector;
                    found = true;

                    if (source.Length == EmbeddingDimensions.DIM)
                    {
                        return source;
                    }

                    var normalizedGeneric = new float[EmbeddingDimensions.DIM];
                    var lengthGeneric = Math.Min(source.Length, normalizedGeneric.Length);
                    Array.Copy(source, normalizedGeneric, lengthGeneric);
                    return normalizedGeneric;
                }
            }
            catch
            {
                // Ignore errors and fall back to legacy candidate loading.
            }

            // 2) Fall back to legacy mini-insurance candidate.
            var candidate = LoadLatestCandidate(baseDirectory, out found);
            if (!found || candidate?.DeltaVector == null || candidate.DeltaVector.Length == 0)
            {
                return new float[EmbeddingDimensions.DIM];
            }

            var sourceCandidate = candidate.DeltaVector;
            if (sourceCandidate.Length == EmbeddingDimensions.DIM)
            {
                return sourceCandidate;
            }

            var normalized = new float[EmbeddingDimensions.DIM];
            var length = Math.Min(sourceCandidate.Length, normalized.Length);
            Array.Copy(sourceCandidate, normalized, length);
            return normalized;
        }
    }
}
