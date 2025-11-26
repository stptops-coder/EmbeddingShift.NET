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
        /// Loads the latest Delta vector from mini-insurance-first-delta-training_*
        /// directories under the given base directory. If no candidate is found,
        /// returns a zero vector of EmbeddingDimensions.DIM and sets found=false.
        /// </summary>
        public static float[] LoadLatestDeltaVectorOrDefault(
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
                return new float[EmbeddingDimensions.DIM];
            }

            // Sort by name (timestamp prefix) descending → newest first.
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
                    if (candidate?.DeltaVector != null && candidate.DeltaVector.Length > 0)
                    {
                        found = true;
                        return candidate.DeltaVector;
                    }
                }
                catch
                {
                    // Ignore malformed candidates and continue with the next directory.
                }
            }

            found = false;
            return new float[EmbeddingDimensions.DIM];
        }

        /// <summary>
        /// Convenience overload using the standard insurance results root.
        /// </summary>
        public static float[] LoadLatestDeltaVectorOrDefault(out bool found)
        {
            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");
            return LoadLatestDeltaVectorOrDefault(baseDir, out found);
        }
    }
}
