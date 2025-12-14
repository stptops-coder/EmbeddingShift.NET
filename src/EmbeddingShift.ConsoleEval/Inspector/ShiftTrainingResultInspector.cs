using System;
using System.IO;
using System.Text.Json;
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
    public static void PrintHistory(string workflowName, string rootDirectory, int maxItems = 20)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
            throw new ArgumentException("Workflow name must not be null or empty.", nameof(workflowName));
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Root directory must not be null or empty.", nameof(rootDirectory));

        Console.WriteLine($"[ShiftTraining] History for workflow '{workflowName}'");
        Console.WriteLine($"[ShiftTraining] Root directory: {rootDirectory}");
        Console.WriteLine();

        if (!Directory.Exists(rootDirectory))
        {
            Console.WriteLine("[ShiftTraining] Root directory does not exist.");
            return;
        }

        var pattern = $"{workflowName}-training_*";
        var directories = Directory.GetDirectories(rootDirectory, pattern, SearchOption.TopDirectoryOnly);
        if (directories.Length == 0)
        {
            Console.WriteLine("[ShiftTraining] No training result directories found.");
            return;
        }

        Array.Sort(directories, StringComparer.Ordinal);
        Array.Reverse(directories);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        Console.WriteLine("Idx | Created (UTC)         | Runs | dFirst  | dFirst+Δ | dΔvsFirst | Scope");
        Console.WriteLine("----+-----------------------+------+---------+----------+-----------+---------");

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

            var created = result.CreatedUtc;
            var runs = result.ComparisonRuns;
            var dFirst = result.ImprovementFirst;
            var dFirstPlusDelta = result.ImprovementFirstPlusDelta;
            var dDelta = result.DeltaImprovement;

            var scopeId = string.IsNullOrWhiteSpace(result.ScopeId) ? "-" : result.ScopeId;

            Console.WriteLine(
                $"{printed,3} | {created:yyyy-MM-ddTHH:mm:ss}   | {runs,4} | {dFirst,7:0.000;-0.000;0.000} | {dFirstPlusDelta,9:0.000;-0.000;0.000} | {dDelta,9:0.000;-0.000;0.000} | {scopeId}");

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
    /// based on the combined improvement (First+Delta vs Baseline).
    /// Falls back to ImprovementFirst if ImprovementFirstPlusDelta is zero.
    /// </summary>
    /// <param name="workflowName">Logical workflow name, e.g. "mini-insurance-first-delta".</param>
    /// <param name="rootDirectory">Root directory where training results are stored.</param>
    public static void PrintBest(string workflowName, string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
            throw new ArgumentException("Workflow name must not be null or empty.", nameof(workflowName));
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Root directory must not be null or empty.", nameof(rootDirectory));

        Console.WriteLine($"[ShiftTraining] Best training result for workflow '{workflowName}'");
        Console.WriteLine($"[ShiftTraining] Root directory: {rootDirectory}");
        Console.WriteLine();

        if (!Directory.Exists(rootDirectory))
        {
            Console.WriteLine("[ShiftTraining] Root directory does not exist.");
            return;
        }

        var pattern = $"{workflowName}-training_*";
        var directories = Directory.GetDirectories(rootDirectory, pattern, SearchOption.TopDirectoryOnly);
        if (directories.Length == 0)
        {
            Console.WriteLine("[ShiftTraining] No training result directories found.");
            return;
        }

        Array.Sort(directories, StringComparer.Ordinal);
        Array.Reverse(directories);

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

            // Score: primarily ImprovementFirstPlusDelta, fallback to ImprovementFirst.
            var score = result.ImprovementFirstPlusDelta;
            if (Math.Abs(score) < 1e-9)
            {
                score = result.ImprovementFirst;
            }

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
