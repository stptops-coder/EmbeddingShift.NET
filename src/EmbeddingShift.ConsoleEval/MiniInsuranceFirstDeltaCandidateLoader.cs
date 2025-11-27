using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Infrastructure;
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
            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");
            return LoadLatestCandidate(baseDir, out found);
        }


        /// <summary>
        /// Convenience overload using the standard insurance results root.
        /// </summary>
        public static float[] LoadLatestDeltaVectorOrDefault(
            string baseDirectory,
            out bool found)
        {
            var candidate = LoadLatestCandidate(baseDirectory, out found);
            if (!found || candidate?.DeltaVector == null || candidate.DeltaVector.Length == 0)
            {
                return new float[EmbeddingDimensions.DIM];
            }

            var source = candidate.DeltaVector;
            if (source.Length == EmbeddingDimensions.DIM)
            {
                return source;
            }

            var normalized = new float[EmbeddingDimensions.DIM];
            var length = Math.Min(source.Length, normalized.Length);
            Array.Copy(source, normalized, length);
            return normalized;
        }

    }
}
