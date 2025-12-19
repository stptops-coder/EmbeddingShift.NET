using System;
using System.IO;
using System.Text;
using System.Text.Json;
using EmbeddingShift.Abstractions.Shifts;

namespace EmbeddingShift.ConsoleEval.Repositories;

/// <summary>
/// File-system based implementation of <see cref="IShiftTrainingResultRepository"/>.
/// 
/// Layout convention:
///   rootDirectory/
///     {workflowName}-training_{timestamp}/
///       shift-training-result.json
///
/// The exact JSON shape is defined by <see cref="ShiftTrainingResult"/>.
/// </summary>
public sealed class FileSystemShiftTrainingResultRepository : IShiftTrainingResultRepository
{
    private readonly string _rootDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Encoding _utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Creates a new file-system based shift training result repository.
    /// </summary>
    /// <param name="rootDirectory">
    /// Root directory under which training results should be stored.
    /// This directory will be created if it does not exist.
    /// </param>
    public FileSystemShiftTrainingResultRepository(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Root directory must not be null or empty.", nameof(rootDirectory));

        _rootDirectory = rootDirectory;
        Directory.CreateDirectory(_rootDirectory);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }

    /// <inheritdoc />
    public void Save(ShiftTrainingResult result)
    {
        if (result is null)
            throw new ArgumentNullException(nameof(result));

        if (string.IsNullOrWhiteSpace(result.WorkflowName))
            throw new ArgumentException("WorkflowName must be set on the training result.", nameof(result));

        // Ensure we have a sensible timestamp.
        var createdUtc = result.CreatedUtc == default
            ? DateTime.UtcNow
            : result.CreatedUtc;

        // Directory layout: {workflowName}-training_yyyyMMdd_HHmmss_fff
        var stamp = createdUtc.ToString("yyyyMMdd_HHmmss_fff");
        var directoryName = $"{result.WorkflowName}-training_{stamp}";
        var targetDirectory = Path.Combine(_rootDirectory, directoryName);

        Directory.CreateDirectory(targetDirectory);

        var effectiveResult = result with { CreatedUtc = createdUtc };

        var jsonPath = Path.Combine(targetDirectory, "shift-training-result.json");
        var json = JsonSerializer.Serialize(effectiveResult, _jsonOptions);
        File.WriteAllText(jsonPath, json, _utf8NoBom);

        var markdownPath = Path.Combine(targetDirectory, "shift-training-result.md");
        var markdown = BuildMarkdown(effectiveResult);
        File.WriteAllText(markdownPath, markdown, _utf8NoBom);
    }
    private static string BuildMarkdown(ShiftTrainingResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Shift Training Result – {result.WorkflowName}");
        sb.AppendLine();
        sb.AppendLine("| Field                      | Value |");
        sb.AppendLine("|---------------------------|-------|");
        sb.AppendLine($"| Created (UTC)             | `{result.CreatedUtc:O}` |");
        sb.AppendLine($"| Base directory            | `{result.BaseDirectory}` |");
        sb.AppendLine($"| Comparison runs           | `{result.ComparisonRuns}` |");

        var modeText = string.IsNullOrWhiteSpace(result.TrainingMode) ? "-" : result.TrainingMode;
        var epsText = result.CancelOutEpsilon > 0 ? result.CancelOutEpsilon.ToString("0.000000E+0") : "-";
        var normText = result.DeltaNorm > 0 ? result.DeltaNorm.ToString("0.000000E+0") : "-";
        var cancelReason = (result.CancelReason ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

        sb.AppendLine($"| Training mode             | `{modeText}` |");
        sb.AppendLine($"| Cancel-out epsilon        | `{epsText}` |");
        sb.AppendLine($"| Cancelled                 | `{result.IsCancelled}` |");
        if (result.IsCancelled && !string.IsNullOrWhiteSpace(cancelReason))
            sb.AppendLine($"| Cancel reason             | `{cancelReason}` |");
        sb.AppendLine($"| Delta L2 norm             | `{normText}` |");

        sb.AppendLine($"| Improvement First         | `{result.ImprovementFirst:+0.000;-0.000;0.000}` |");
        sb.AppendLine($"| Improvement First+Delta   | `{result.ImprovementFirstPlusDelta:+0.000;-0.000;0.000}` |");
        sb.AppendLine($"| Delta improvement vs First| `{result.DeltaImprovement:+0.000;-0.000;0.000}` |");
        sb.AppendLine();

        var vector = result.DeltaVector ?? Array.Empty<float>();
        if (vector.Length == 0)
        {
            sb.AppendLine("Delta vector: *(empty)*");
            return sb.ToString();
        }

        sb.AppendLine("## Top Delta dimensions (by |value|)");
        sb.AppendLine();
        sb.AppendLine("| Index | Value |");
        sb.AppendLine("|-------|-------|");

        var used = new bool[vector.Length];
        const int topN = 8;

        for (var n = 0; n < topN; n++)
        {
            var bestIndex = -1;
            var bestAbs = 0.0f;

            for (var i = 0; i < vector.Length; i++)
            {
                if (used[i])
                    continue;

                var abs = Math.Abs(vector[i]);
                if (abs > bestAbs)
                {
                    bestAbs = abs;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || bestAbs <= 0.0f)
            {
                break;
            }

            used[bestIndex] = true;
            sb.AppendLine($"| {bestIndex} | {vector[bestIndex]:+0.000;-0.000;0.000} |");
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public ShiftTrainingResult? LoadLatest(string workflowName)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
            throw new ArgumentException("Workflow name must not be null or empty.", nameof(workflowName));

        if (!Directory.Exists(_rootDirectory))
            return null;

        // Pattern: {workflowName}-training_*
        var prefix = $"{workflowName}-training_";
        var candidates = Directory.GetDirectories(_rootDirectory, $"{workflowName}-training_*", SearchOption.TopDirectoryOnly);

        if (candidates.Length == 0)
            return null;

        Array.Sort(candidates, StringComparer.Ordinal);
        Array.Reverse(candidates);

        foreach (var dir in candidates)
        {
            var jsonPath = Path.Combine(dir, "shift-training-result.json");

            // Legacy fallback (older runs might have used a different filename).
            if (!File.Exists(jsonPath))
            {
                var legacyPath = Path.Combine(dir, "result.json");
                if (!File.Exists(legacyPath))
                    continue;

                jsonPath = legacyPath;
            }

            try
            {
                var json = File.ReadAllText(jsonPath, _utf8NoBom);
                var result = JsonSerializer.Deserialize<ShiftTrainingResult>(json, _jsonOptions);
                if (result != null)
                    return result;
            }
            catch
            {
                // Ignore malformed entries and continue with older ones.
            }
        }

        return null;
    }

    /// <inheritdoc />
    public ShiftTrainingResult? LoadBest(string workflowName, bool includeCancelled = false)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
            throw new ArgumentException("Workflow name must not be null or empty.", nameof(workflowName));

        if (!Directory.Exists(_rootDirectory))
            return null;

        // Pattern: {workflowName}-training_*
        var candidates = Directory.GetDirectories(
            _rootDirectory,
            $"{workflowName}-training_*",
            SearchOption.TopDirectoryOnly);

        if (candidates.Length == 0)
            return null;

        ShiftTrainingResult? best = null;
        double bestScore = double.NegativeInfinity;
        DateTime bestCreatedUtc = DateTime.MinValue;

        foreach (var dir in candidates)
        {
            var jsonPath = Path.Combine(dir, "shift-training-result.json");

            // Legacy fallback (older runs might have used a different filename).
            if (!File.Exists(jsonPath))
            {
                var legacyPath = Path.Combine(dir, "result.json");
                if (!File.Exists(legacyPath))
                    continue;

                jsonPath = legacyPath;
            }

            try
            {
                var json = File.ReadAllText(jsonPath, _utf8NoBom);
                var result = JsonSerializer.Deserialize<ShiftTrainingResult>(json, _jsonOptions);
                if (result is null)
                    continue;

                if (!includeCancelled && result.IsCancelled)
                    continue;

                // Primary score: First+Delta improvement; fallback: First improvement.
                var score = (double)result.ImprovementFirstPlusDelta;
                if (Math.Abs(score) < 1e-12)
                    score = (double)result.ImprovementFirst;

                var createdUtc = result.CreatedUtc;

                // Prefer higher score; break ties by most recent creation time.
                if (best is null ||
                    score > bestScore + 1e-12 ||
                    (Math.Abs(score - bestScore) < 1e-12 && createdUtc > bestCreatedUtc))
                {
                    best = result;
                    bestScore = score;
                    bestCreatedUtc = createdUtc;
                }
            }
            catch
            {
                // Ignore malformed entries and continue.
            }
        }

        return best;
    }
}
