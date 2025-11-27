using System;
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
}
