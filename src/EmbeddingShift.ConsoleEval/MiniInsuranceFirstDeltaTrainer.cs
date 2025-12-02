using System;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.ConsoleEval.Repositories;
using System.IO;
using System.Text;
using System.Text.Json;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Workflows;

namespace EmbeddingShift.ConsoleEval
{
    /// <summary>
    /// Result of a simple training step for the mini-insurance First/Delta
    /// configuration. This is designed to be DB-ready: it contains only
    /// primitive fields plus the proposed delta vector.
    /// </summary>
    public sealed class MiniInsuranceShiftTrainingResult
    {
        public DateTime CreatedUtc { get; init; }
        public string BaseDirectory { get; init; } = string.Empty;
        public int ComparisonRuns { get; init; }

        /// <summary>
        /// Combined improvement of FirstShift vs baseline
        /// (weighted map@1 / ndcg@3).
        /// </summary>
        public double ImprovementFirst { get; init; }

        /// <summary>
        /// Combined improvement of First+Delta vs baseline
        /// (weighted map@1 / ndcg@3).
        /// </summary>
        public double ImprovementFirstPlusDelta { get; init; }

        /// <summary>
        /// Proposed delta vector in the 7D keyword embedding space.
        /// Layout: 0=fire,1=water,2=damage,3=theft,4=claims,5=flood,6=storm.
        /// </summary>
        public float[] DeltaVector { get; init; } = Array.Empty<float>();

        /// <summary>
        /// Difference between First+Delta and First (combined metric).
        /// Positive values mean the Delta component helps, negative values
        /// mean it hurts.
        /// </summary>
        public double DeltaImprovement => ImprovementFirstPlusDelta - ImprovementFirst;
    }

    /// <summary>
    /// Simple trainer that looks at aggregated mini-insurance metrics
    /// and proposes a new delta vector. This is intentionally naive,
    /// but illustrates "Delta über Delta": metrics drive the next shift.
    /// </summary>
    internal static class MiniInsuranceFirstDeltaTrainer
    {
        public static MiniInsuranceShiftTrainingResult TrainFromAggregate(
            string baseDirectory,
            MiniInsuranceFirstDeltaAggregate aggregate)
        {
            if (aggregate == null) throw new ArgumentNullException(nameof(aggregate));

            MiniInsuranceAggregateMetricRow? mapRow = null;
            MiniInsuranceAggregateMetricRow? ndcgRow = null;

            foreach (var row in aggregate.Metrics)
            {
                if (string.Equals(row.Metric, "map@1", StringComparison.OrdinalIgnoreCase))
                {
                    mapRow = row;
                }
                else if (string.Equals(row.Metric, "ndcg@3", StringComparison.OrdinalIgnoreCase))
                {
                    ndcgRow = row;
                }
            }

            if (mapRow == null || ndcgRow == null)
            {
                throw new InvalidOperationException(
                    "Expected metrics 'map@1' and 'ndcg@3' to be present in aggregate.");
            }

            const double MapWeight = 0.7;
            const double NdcgWeight = 0.3;

            var improvementFirst =
                MapWeight * mapRow.AverageDeltaFirstVsBaseline +
                NdcgWeight * ndcgRow.AverageDeltaFirstVsBaseline;

            var improvementFirstPlusDelta =
                MapWeight * mapRow.AverageDeltaFirstPlusDeltaVsBaseline +
                NdcgWeight * ndcgRow.AverageDeltaFirstPlusDeltaVsBaseline;

            var deltaImprovement = improvementFirstPlusDelta - improvementFirst;

            // Build a new delta vector in the 1536D keyword-based space.
            // FileBasedInsuranceMiniWorkflow's local provider uses the same
            // EmbeddingDimensions.DIM layout (first 7 dimensions are the
            // insurance keywords, the remaining dimensions stay zero).
            var deltaVector = new float[EmbeddingDimensions.DIM];

            // Domain layout:
            // 0: fire, 1: water, 2: damage, 3: theft, 4: claims, 5: flood, 6: storm.

            // Base magnitude for the flood/storm delta component.
            const float BaseMagnitude = 0.5f;
            const double Epsilon = 1e-6;

            // Use the combined First+Delta improvement as primary driver for
            // the delta magnitude. We clamp it into a reasonable range to
            // avoid exploding shifts on noisy metrics.
            var absImprovement = Math.Abs(improvementFirstPlusDelta);
            var clamped = Math.Min(absImprovement, 0.2); // up to 0.2

            // Map [0, 0.2] -> [0.5, 1.5] as a scaling factor.
            var scale = 0.5 + (clamped / 0.2); // 0.5 .. 1.5

            // If the trained Delta performs worse than First alone, dampen it.
            if (deltaImprovement < -Epsilon)
            {
                scale *= 0.5;
            }

            var magnitude = BaseMagnitude * (float)scale;

            // Flood and storm dimensions receive the same learned magnitude.
            deltaVector[5] = magnitude;
            deltaVector[6] = magnitude;

            return new MiniInsuranceShiftTrainingResult
            {
                CreatedUtc = DateTime.UtcNow,
                BaseDirectory = baseDirectory,
                ComparisonRuns = aggregate.ComparisonCount,
                ImprovementFirst = improvementFirst,
                ImprovementFirstPlusDelta = improvementFirstPlusDelta,
                DeltaVector = deltaVector
            };
        }

        public static string PersistCandidate(
            string baseDirectory,
            MiniInsuranceShiftTrainingResult candidate)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("Base directory must not be null or empty.", nameof(baseDirectory));

            var runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var trainingDir = Path.Combine(baseDirectory, $"mini-insurance-first-delta-training_{runId}");

            Directory.CreateDirectory(trainingDir);

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            var jsonPath = Path.Combine(trainingDir, "shift-candidate.json");
            var json = JsonSerializer.Serialize(candidate, jsonOptions);
            File.WriteAllText(jsonPath, json, encoding);

            var markdownPath = Path.Combine(trainingDir, "shift-candidate.md");
            var markdown = BuildMarkdown(candidate);
            File.WriteAllText(markdownPath, markdown, encoding);

            // Additionally persist a generic ShiftTrainingResult representation
            // using the shared abstraction and file-system repository.
            var genericResult = new ShiftTrainingResult
            {
                WorkflowName = "mini-insurance-first-delta",
                CreatedUtc = candidate.CreatedUtc,
                BaseDirectory = candidate.BaseDirectory,
                ComparisonRuns = candidate.ComparisonRuns,
                ImprovementFirst = candidate.ImprovementFirst,
                ImprovementFirstPlusDelta = candidate.ImprovementFirstPlusDelta,
                DeltaImprovement = candidate.DeltaImprovement,
                DeltaVector = candidate.DeltaVector ?? Array.Empty<float>(),
                ScopeId = "default"
            };

            var genericRepo = new FileSystemShiftTrainingResultRepository(baseDirectory);
            genericRepo.Save(genericResult);

            return trainingDir;
        }

        private static string BuildMarkdown(MiniInsuranceShiftTrainingResult candidate)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# Mini Insurance Delta Shift Training Result");
            sb.AppendLine();
            sb.AppendLine($"Created (UTC): {candidate.CreatedUtc:O}");
            sb.AppendLine();
            sb.AppendLine($"Base directory: `{candidate.BaseDirectory}`");
            sb.AppendLine();
            sb.AppendLine($"Comparison runs used: {candidate.ComparisonRuns}");
            sb.AppendLine();
            sb.AppendLine($"Combined First improvement:       {candidate.ImprovementFirst:+0.000;-0.000;0.000}");
            sb.AppendLine($"Combined First+Delta improvement: {candidate.ImprovementFirstPlusDelta:+0.000;-0.000;0.000}");
            sb.AppendLine($"Delta improvement vs First:       {candidate.DeltaImprovement:+0.000;-0.000;0.000}");
            sb.AppendLine();
            sb.AppendLine("## Proposed Delta Vector (index: value)");
            sb.AppendLine();

            for (int i = 0; i < candidate.DeltaVector.Length; i++)
            {
                sb.AppendLine($"- [{i}] = {candidate.DeltaVector[i]:+0.000;-0.000;0.000}");
            }

            sb.AppendLine();
            return sb.ToString();
        }
    }
}
