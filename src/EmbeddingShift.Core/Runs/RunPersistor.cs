using System;
using System.IO;
using System.Text.Json;
using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Core.Runs
{
    /// <summary>
    /// Persists run artifacts (report + manifest) to disk so runs
    /// are reproducible and inspectable.
    /// </summary>
    public static class RunPersistor
    {
        public static string Persist(string rootDir, RunArtifacts artifacts)
        {
            if (artifacts is null) throw new ArgumentNullException(nameof(artifacts));

            var baseDir = string.IsNullOrWhiteSpace(rootDir) ? "runs" : rootDir;
            Directory.CreateDirectory(baseDir);

            var dirName = $"{artifacts.Started:yyyyMMdd_HHmmss}_{SanitizeFile(artifacts.Workflow)}";
            var runDir = Path.Combine(baseDir, dirName);
            Directory.CreateDirectory(runDir);

            // Markdown report
            var reportPath = Path.Combine(runDir, "RunReport.md");
            File.WriteAllText(reportPath, artifacts.ReportMarkdown);

            // JSON manifest
            var manifest = new
            {
                artifacts.RunId,
                artifacts.RunName,
                artifacts.Workflow,
                artifacts.Started,
                artifacts.Finished,
                artifacts.Success,
                artifacts.Notes,
                artifacts.ErrorMessage,
                Metrics = artifacts.Metrics
            };

            var json = JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions { WriteIndented = true });

            var manifestPath = Path.Combine(runDir, "RunManifest.json");
            File.WriteAllText(manifestPath, json);

            return runDir;
        }

        private static string SanitizeFile(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }
    }
}
