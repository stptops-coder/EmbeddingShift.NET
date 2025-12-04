using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace EmbeddingShift.ConsoleEval.Commands
{
    /// <summary>
    /// Inspects the latest Mini-Insurance training result by scanning
    /// local/mini-insurance/training/history and printing a compact summary.
    /// </summary>
    internal static class MiniInsuranceTrainingInspectCommand
    {
        public static Task RunAsync(string[] args)
        {
            Console.WriteLine("[Mini-Insurance · Training Inspect]");
            Console.WriteLine();

            var basePath = Path.Combine(AppContext.BaseDirectory, "local", "mini-insurance");
            Console.WriteLine($"Base path:       {basePath}");

            var historyRoot = Path.Combine(basePath, "training", "history");
            Console.WriteLine($"History folder:  {historyRoot}");
            Console.WriteLine();

            if (!Directory.Exists(historyRoot))
            {
                Console.WriteLine("No training history found (folder does not exist).");
                return Task.CompletedTask;
            }

            var historyDir = new DirectoryInfo(historyRoot);
            var latestDir = historyDir
                .GetDirectories()
                .OrderByDescending(d => d.CreationTimeUtc)
                .FirstOrDefault();

            if (latestDir == null)
            {
                Console.WriteLine("No training history found (no subdirectories).");
                return Task.CompletedTask;
            }

            Console.WriteLine($"Latest run dir:  {latestDir.FullName}");

            var jsonFile = latestDir
                .GetFiles("*.json")
                .OrderByDescending(f => f.CreationTimeUtc)
                .FirstOrDefault();

            if (jsonFile == null)
            {
                Console.WriteLine("No JSON file found in latest training directory.");
                return Task.CompletedTask;
            }

            Console.WriteLine($"Using JSON file: {jsonFile.FullName}");
            Console.WriteLine();

            try
            {
                PrintJsonSummary(jsonFile.FullName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to read or parse training-result JSON.");
                Console.WriteLine(ex.Message);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Prints a generic summary of the top-level JSON properties and
        /// then a truncated raw JSON preview.
        /// </summary>
        private static void PrintJsonSummary(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;

            Console.WriteLine("Top-level properties:");
            Console.WriteLine("---------------------");

            foreach (var prop in root.EnumerateObject())
            {
                string info;

                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        var s = prop.Value.GetString() ?? string.Empty;
                        if (s.Length > 80)
                        {
                            s = s.Substring(0, 77) + "...";
                        }
                        info = $"\"{s}\"";
                        break;

                    case JsonValueKind.Number:
                        info = prop.Value.ToString();
                        break;

                    case JsonValueKind.Array:
                        info = $"Array (length {prop.Value.GetArrayLength()})";
                        break;

                    case JsonValueKind.Object:
                        info = "Object";
                        break;

                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        info = prop.Value.GetBoolean().ToString();
                        break;

                    case JsonValueKind.Null:
                        info = "null";
                        break;

                    default:
                        info = "(other)";
                        break;
                }

                Console.WriteLine($"- {prop.Name}: {info}");
            }

            Console.WriteLine();
            Console.WriteLine("Raw JSON (truncated preview):");
            Console.WriteLine("------------------------------");

            var preview = json;
            const int maxPreviewLength = 800;

            if (preview.Length > maxPreviewLength)
            {
                preview = preview.Substring(0, maxPreviewLength) + Environment.NewLine + "... (truncated) ...";
            }

            Console.WriteLine(preview);
        }
    }
}
