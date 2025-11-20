using System;
using System.IO;
using System.Text;
using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Core.Runs
{
    /// <summary>
    /// Very small helper that persists a <see cref="WorkflowResult"/>
    /// as a Markdown report into a timestamped run directory.
    ///
    /// This is intentionally minimal and only used by smoke and
    /// pipeline tests. More advanced run persistence can be added later
    /// without changing this contract.
    /// </summary>
    public static class RunPersistor
    {
        /// <summary>
        /// Persists the given <paramref name="result"/> as "report.md" into
        /// a new subdirectory below <paramref name="baseDirectory"/>.
        /// Returns the absolute path of the created run directory.
        /// </summary>
        public static string Persist(string baseDirectory, WorkflowResult result)
        {
            if (baseDirectory is null)
                throw new ArgumentNullException(nameof(baseDirectory));

            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("Base directory must not be empty.", nameof(baseDirectory));

            if (result is null)
                throw new ArgumentNullException(nameof(result));

            var workflowName = result.Workflow();
            if (string.IsNullOrWhiteSpace(workflowName))
                workflowName = "workflow";

            var runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var directoryName = $"{SanitizeFileName(workflowName)}_{runId}";
            var runDirectory = Path.Combine(baseDirectory, directoryName);

            Directory.CreateDirectory(runDirectory);

            var reportPath = Path.Combine(runDirectory, "report.md");
            var markdown = result.ReportMarkdown();

            File.WriteAllText(
                reportPath,
                markdown,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            return runDirectory;
        }

        private static string SanitizeFileName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);

            foreach (var ch in name)
            {
                sb.Append(Array.IndexOf(invalidChars, ch) >= 0 ? '_' : ch);
            }

            return sb.ToString();
        }
    }
}
