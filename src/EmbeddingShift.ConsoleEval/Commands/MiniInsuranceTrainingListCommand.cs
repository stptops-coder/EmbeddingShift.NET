using EmbeddingShift.ConsoleEval.MiniInsurance;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EmbeddingShift.ConsoleEval.Commands
{
    internal static class MiniInsuranceTrainingListCommand
    {
        public static Task RunAsync(string[] args)
        {
            Console.WriteLine();
            Console.WriteLine("=== Mini-Insurance · Training Runs (history) ===");
            Console.WriteLine();

            // Old:
            // var basePath = MiniInsurancePaths.GetBasePath();
            // var historyRoot = Path.Combine(basePath, "training");

            // New: use dedicated training root from MiniInsurancePaths
            // Use the MiniInsurancePaths training root.
            // If a "history" subfolder exists, prefer that (older layouts).
            var trainingRoot = MiniInsurancePaths.GetTrainingRoot();
            var historyRoot = Path.Combine(trainingRoot, "history");
            var effectiveRoot = Directory.Exists(historyRoot) ? historyRoot : trainingRoot;

            var runDirs = new DirectoryInfo(effectiveRoot)
                .GetDirectories()
                .OrderByDescending(d => d.CreationTimeUtc)
                .ToList();

            if (runDirs.Count == 0)
            {
                Console.WriteLine("[INFO] No training runs found.");
                return Task.CompletedTask;
            }

            int index = 1;
            foreach (var dir in runDirs)
            {
                var jsonFile = dir.GetFiles("*.json")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();

                if (jsonFile == null)
                {
                    Console.WriteLine($"{index,2}. {dir.Name}   (no JSON file)");
                    index++;
                    continue;
                }

                string metricsSummary;
                try
                {
                    var json = File.ReadAllText(jsonFile.FullName);
                    using var doc = JsonDocument.Parse(json);
                    metricsSummary = BuildMetricSummary(doc.RootElement);
                }
                catch (Exception ex)
                {
                    metricsSummary = $"(failed to read JSON: {ex.Message})";
                }

                if (string.IsNullOrWhiteSpace(metricsSummary))
                {
                    Console.WriteLine($"{index,2}. {dir.Name}");
                }
                else
                {
                    Console.WriteLine($"{index,2}. {dir.Name}  |  {metricsSummary}");
                }

                index++;
            }

            Console.WriteLine();
            Console.WriteLine($"[INFO] Listed {runDirs.Count} training runs from:");
            Console.WriteLine($"       {historyRoot}");
            Console.WriteLine();

            return Task.CompletedTask;
        }

        private static string BuildMetricSummary(JsonElement root)
        {
            var parts = new List<string>();

            foreach (var prop in root.EnumerateObject())
            {
                var nameLower = prop.Name.ToLowerInvariant();

                // Heuristik: typische Metrik-Namen herausfiltern
                bool isMetricName =
                    nameLower.Contains("map") ||
                    nameLower.Contains("ndcg") ||
                    nameLower.Contains("score");

                if (!isMetricName)
                    continue;

                string valueString = prop.Value.ValueKind switch
                {
                    JsonValueKind.Number => prop.Value.ToString(),
                    JsonValueKind.String => prop.Value.GetString() ?? "",
                    _ => prop.Value.ToString()
                };

                if (!string.IsNullOrWhiteSpace(valueString))
                {
                    parts.Add($"{prop.Name}={valueString}");
                }

                if (parts.Count >= 4)
                {
                    break; // nicht zu viel pro Zeile
                }
            }

            return string.Join(", ", parts);
        }
    }
}
