using System;
using System.IO;
using System.Text.Json;
using System.Linq;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.ConsoleEval.Repositories;

namespace EmbeddingShift.ConsoleEval.Inspector;

/// <summary>
/// Helper for inspecting shift training results in a generic way.
/// This is independent of any specific domain (e.g. mini-insurance)
/// and can be wired up to console commands as needed.
/// </summary>
internal static class ShiftTrainingResultInspector
{
    private static string NormalizeDirectory(string path)
    {
        return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsTrainingDirectory(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.Equals(name, "training", StringComparison.OrdinalIgnoreCase);
    }

    private static (string TrainingRoot, string LegacyRoot) ResolveTrainingAndLegacyRoots(string rootDirectory)
    {
        var normalized = NormalizeDirectory(rootDirectory);
        if (IsTrainingDirectory(normalized))
        {
            var legacy = Path.GetDirectoryName(normalized) ?? normalized;
            return (normalized, legacy);
        }

        return (Path.Combine(normalized, "training"), normalized);
    }

    private static string[] GetTrainingResultDirectories(string rootDirectory, string pattern)
    {
        var (trainingRoot, legacyRoot) = ResolveTrainingAndLegacyRoots(rootDirectory);

        var dirs = new System.Collections.Generic.List<string>();

        if (Directory.Exists(trainingRoot))
            dirs.AddRange(Directory.GetDirectories(trainingRoot, pattern, SearchOption.TopDirectoryOnly));
        if (Directory.Exists(legacyRoot) &&
            !string.Equals(trainingRoot, legacyRoot, StringComparison.OrdinalIgnoreCase))
        {
            dirs.AddRange(Directory.GetDirectories(legacyRoot, pattern, SearchOption.TopDirectoryOnly));
        }
        // De-dup, then sort newest-first (directory names include sortable timestamps).
        var distinct = dirs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Array.Sort(distinct, StringComparer.Ordinal);
        Array.Reverse(distinct);

        return distinct;
    }

    /// <summary>
    /// Loads and prints the latest training result for the specified
    /// workflow from the given root directory. If no result is found,
    /// a short message is printed instead.
    /// </summary>
    /// <param name="workflowName">Logical workflow name, e.g. "mini-insurance-first-delta".</param>
    /// <param name="rootDirectory">
    /// Root directory where training results are stored. Typically the
    /// same root that is passed to <see cref="FileSystemShiftTrainingResultRepository"/>.
    /// </param>
    public static void PrintLatest(string workflowName, string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
        {
            throw new ArgumentException("Workflow name must not be null or empty.", nameof(workflowName));
        }

        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Root directory must not be null or empty.", nameof(rootDirectory));
        }

        Console.WriteLine($"[ShiftTraining] Inspecting latest training result for workflow '{workflowName}'...");
        Console.WriteLine($"[ShiftTraining] Root directory: {rootDirectory}");
        Console.WriteLine();

        var repository = new FileSystemShiftTrainingResultRepository(rootDirectory);
        ShiftTrainingResult? result;

        try
        {
            result = repository.LoadLatest(workflowName);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ShiftTraining] Error while loading latest training result:");
            Console.WriteLine($"  {ex.Message}");
            return;
        }

        if (result is null)
        {
            Console.WriteLine("[ShiftTraining] No training result found.");
            Console.WriteLine("  Make sure training has been executed for this workflow.");
            return;
        }

        Console.WriteLine($"Workflow       : {result.WorkflowName}");
        Console.WriteLine($"Created (UTC)  : {result.CreatedUtc:O}");
        Console.WriteLine($"Base directory : {result.BaseDirectory}");
        Console.WriteLine($"Runs           : {result.ComparisonRuns}");
        Console.WriteLine($"Improvement First         : {result.ImprovementFirst:+0.000;-0.000;0.000}");
        Console.WriteLine($"Improvement First+Delta   : {result.ImprovementFirstPlusDelta:+0.000;-0.000;0.000}");
        Console.WriteLine($"Delta improvement vs First: {result.DeltaImprovement:+0.000;-0.000;0.000}");
        if (result.SelectionScore.HasValue)
        {
            Console.WriteLine($"Selection score           : {result.SelectionScore.Value:+0.000;-0.000;0.000}");
            if (result.SelectionMapAt1Baseline.HasValue && result.SelectionMapAt1Shifted.HasValue)
            {
                var deltaMap = result.SelectionMapAt1Shifted.Value - result.SelectionMapAt1Baseline.Value;
                Console.WriteLine($"Selection MAP@1           : {result.SelectionMapAt1Baseline.Value:0.000} -> {result.SelectionMapAt1Shifted.Value:0.000} ({deltaMap:+0.000;-0.000;0.000})");
            }
            if (result.SelectionNdcg3Baseline.HasValue && result.SelectionNdcg3Shifted.HasValue)
            {
                var deltaNdcg = result.SelectionNdcg3Shifted.Value - result.SelectionNdcg3Baseline.Value;
                Console.WriteLine($"Selection NDCG@3          : {result.SelectionNdcg3Baseline.Value:0.000} -> {result.SelectionNdcg3Shifted.Value:0.000} ({deltaNdcg:+0.000;-0.000;0.000})");
            }
        }
        Console.WriteLine();

        var vector = result.DeltaVector ?? Array.Empty<float>();
        if (vector.Length == 0)
        {
            Console.WriteLine("Delta vector: (empty)");
            Console.WriteLine();
            Console.WriteLine("[ShiftTraining] Inspection done.");
            return;
        }

        Console.WriteLine("Top Delta dimensions (by |value|):");

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
            Console.WriteLine($"  [{bestIndex}] = {vector[bestIndex]:+0.000;-0.000;0.000}");
        }

        Console.WriteLine();
        Console.WriteLine("[ShiftTraining] Inspection done.");
    }
    /// <summary>
    /// Lists the latest training results for the specified workflow in
    /// descending order (newest first).
    /// </summary>
    /// <param name="workflowName">Logical workflow name, e.g. "mini-insurance-first-delta".</param>
    /// <param name="rootDirectory">Root directory where training results are stored.</param>
    /// <param name="maxItems">Maximum number of results to print.</param>
    public static void PrintHistory(string workflowName, string rootDirectory, int maxItems = 20, bool includeCancelled = false)

    {
        if (string.IsNullOrWhiteSpace(workflowName))
            throw new ArgumentException("Workflow name must not be null or empty.", nameof(workflowName));
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Root directory must not be null or empty.", nameof(rootDirectory));

        Console.WriteLine($"[ShiftTraining] History for workflow '{workflowName}'");
        Console.WriteLine($"[ShiftTraining] Root directory: {rootDirectory}");
        Console.WriteLine();
        var pattern = $"{workflowName}-training_*";
        var directories = GetTrainingResultDirectories(rootDirectory, pattern);
        if (directories.Length == 0)
        {
            Console.WriteLine("[ShiftTraining] No training result directories found.");
            return;
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        Console.WriteLine("Idx | Created (UTC)         | Runs | Score   | dFirst  | dFirst+Δ | Scope");
        Console.WriteLine("----+-----------------------+------+---------+---------+----------+---------");

        var printed = 0;

        foreach (var dir in directories)
        {
            if (printed >= maxItems)
                break;

            var jsonPath = Path.Combine(dir, "shift-training-result.json");
            if (!File.Exists(jsonPath))
                continue;

            ShiftTrainingResult? result;
            try
            {
                var json = File.ReadAllText(jsonPath);
                result = JsonSerializer.Deserialize<ShiftTrainingResult>(json, jsonOptions);
            }
            catch
            {
                // Ignore malformed entries.
                continue;
            }

            if (result is null)
                continue;

            if (!includeCancelled && result.IsCancelled)
                continue;

            var created = result.CreatedUtc;
            var runs = result.ComparisonRuns;
            var score = ShiftTrainingResultScoring.GetPreferredScore(result);
            var dFirst = result.ImprovementFirst;
            var dFirstPlusDelta = result.ImprovementFirstPlusDelta;

            var scopeId = string.IsNullOrWhiteSpace(result.ScopeId) ? "-" : result.ScopeId;

            var createdText = created.ToString("yyyy-MM-ddTHH:mm:ss.fff");

            if (includeCancelled && result.IsCancelled)
                scopeId += " [C]";

            Console.WriteLine(
                $"{printed,3} | {createdText} | {runs,4} | {score,7:0.000;-0.000;0.000} | {dFirst,7:0.000;-0.000;0.000} | {dFirstPlusDelta,8:0.000;-0.000;0.000} | {scopeId}");

            printed++;
        }

        if (printed == 0)
        {
            Console.WriteLine("[ShiftTraining] No valid training results found.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"[ShiftTraining] Listed {printed} training result(s).");
    }
    /// <summary>
    /// Finds and prints the best training result for the specified workflow
    /// based on the preferred persisted score. Legacy First/First+Delta
    /// runs use their historical improvements; newer modes (for example PosNeg)
    /// can persist an explicit selection score.
    /// </summary>
    /// <param name="workflowName">Logical workflow name, e.g. "mini-insurance-first-delta".</param>
    /// <param name="rootDirectory">Root directory where training results are stored.</param>
    public static void PrintBest(string workflowName, string rootDirectory, bool includeCancelled = false)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
            throw new ArgumentException("Workflow name must not be null or empty.", nameof(workflowName));
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Root directory must not be null or empty.", nameof(rootDirectory));

        Console.WriteLine($"[ShiftTraining] Best training result for workflow '{workflowName}'");
        Console.WriteLine($"[ShiftTraining] Root directory: {rootDirectory}");
        Console.WriteLine();
        var pattern = $"{workflowName}-training_*";
        var directories = GetTrainingResultDirectories(rootDirectory, pattern);
        if (directories.Length == 0)
        {
            Console.WriteLine("[ShiftTraining] No training result directories found.");
            return;
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        ShiftTrainingResult? bestResult = null;
        string? bestDirectory = null;
        double bestScore = double.NegativeInfinity;

        foreach (var dir in directories)
        {
            var jsonPath = Path.Combine(dir, "shift-training-result.json");
            if (!File.Exists(jsonPath))
                continue;

            ShiftTrainingResult? result;
            try
            {
                var json = File.ReadAllText(jsonPath);
                result = JsonSerializer.Deserialize<ShiftTrainingResult>(json, jsonOptions);
            }
            catch
            {
                // Ignore malformed entries.
                continue;
            }

            if (result is null)
                continue;

            if (!includeCancelled && result.IsCancelled)
                continue;

            var score = ShiftTrainingResultScoring.GetPreferredScore(result);

            if (score > bestScore)
            {
                bestScore = score;
                bestResult = result;
                bestDirectory = dir;
            }
        }

        if (bestResult is null)
        {
            Console.WriteLine("[ShiftTraining] No valid training results found.");
            return;
        }

        Console.WriteLine($"Best directory : {bestDirectory}");
        Console.WriteLine($"Score          : {bestScore:+0.000;-0.000;0.000}");
        Console.WriteLine($"Score source   : {ShiftTrainingResultScoring.GetPreferredScoreSource(bestResult)}");
        Console.WriteLine();
        Console.WriteLine($"Workflow       : {bestResult.WorkflowName}");
        Console.WriteLine($"Created (UTC)  : {bestResult.CreatedUtc:O}");
        Console.WriteLine($"Base directory : {bestResult.BaseDirectory}");
        Console.WriteLine($"Runs           : {bestResult.ComparisonRuns}");
        Console.WriteLine($"ScopeId        : {bestResult.ScopeId}");

        if (!string.IsNullOrWhiteSpace(bestResult.TrainingMode))
            Console.WriteLine($"Mode           : {bestResult.TrainingMode}");
        if (bestResult.CancelOutEpsilon > 0)
            Console.WriteLine($"Cancel epsilon : {bestResult.CancelOutEpsilon:0.000000E+0}");
        if (bestResult.IsCancelled)
            Console.WriteLine($"Cancelled      : true");
        if (bestResult.IsCancelled && !string.IsNullOrWhiteSpace(bestResult.CancelReason))
            Console.WriteLine($"Cancel reason  : {bestResult.CancelReason}");
        if (bestResult.DeltaNorm > 0)
            Console.WriteLine($"Delta norm     : {bestResult.DeltaNorm:0.000000E+0}");

        Console.WriteLine($"Improvement First         : {bestResult.ImprovementFirst:+0.000;-0.000;0.000}");
        Console.WriteLine($"Improvement First+Delta   : {bestResult.ImprovementFirstPlusDelta:+0.000;-0.000;0.000}");
        Console.WriteLine($"Delta improvement vs First: {bestResult.DeltaImprovement:+0.000;-0.000;0.000}");
        if (bestResult.SelectionScore.HasValue)
        {
            Console.WriteLine($"Selection score           : {bestResult.SelectionScore.Value:+0.000;-0.000;0.000}");
            if (bestResult.SelectionMapAt1Baseline.HasValue && bestResult.SelectionMapAt1Shifted.HasValue)
            {
                var deltaMap = bestResult.SelectionMapAt1Shifted.Value - bestResult.SelectionMapAt1Baseline.Value;
                Console.WriteLine($"Selection MAP@1           : {bestResult.SelectionMapAt1Baseline.Value:0.000} -> {bestResult.SelectionMapAt1Shifted.Value:0.000} ({deltaMap:+0.000;-0.000;0.000})");
            }
            if (bestResult.SelectionNdcg3Baseline.HasValue && bestResult.SelectionNdcg3Shifted.HasValue)
            {
                var deltaNdcg = bestResult.SelectionNdcg3Shifted.Value - bestResult.SelectionNdcg3Baseline.Value;
                Console.WriteLine($"Selection NDCG@3          : {bestResult.SelectionNdcg3Baseline.Value:0.000} -> {bestResult.SelectionNdcg3Shifted.Value:0.000} ({deltaNdcg:+0.000;-0.000;0.000})");
            }
        }
        Console.WriteLine();

        var vector = bestResult.DeltaVector ?? Array.Empty<float>();
        if (vector.Length == 0)
        {
            Console.WriteLine("Delta vector: (empty)");
            Console.WriteLine();
            Console.WriteLine("[ShiftTraining] Best result inspection done.");
            return;
        }

        // at this point vector.Length > 0, because the empty case already returned above
        var delta = vector;

        Console.WriteLine();

        // Show overall magnitude of the learned shift vector
        var l2 = Math.Sqrt(delta.Sum(v => (double)v * v));
        Console.WriteLine($"Delta L2 norm (global magnitude): {l2:0.000000E+0}");
        Console.WriteLine();
        Console.WriteLine("Top Delta dimensions (by |value|):");

        var top = delta
            .Select((value, index) => new { Index = index, Value = value })
            .OrderByDescending(x => Math.Abs(x.Value))
            .Take(8);

        foreach (var item in top)
        {
            Console.WriteLine($"  [{item.Index}] = {item.Value:0.000000E+0}");
        }


        Console.WriteLine();
        Console.WriteLine("[ShiftTraining] Best result inspection done.");
    }
}
