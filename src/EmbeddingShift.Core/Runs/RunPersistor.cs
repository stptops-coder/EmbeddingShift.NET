using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Core.Stats;
using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Core.Runs
{
    /// <summary>
    /// Simple facade that delegates execution to StatsAwareWorkflowRunner.
    /// Persistence via IRunRepository can be added here later if needed.
    /// </summary>
    public sealed class RunPersistor
    {
        private readonly StatsAwareWorkflowRunner _runner;
        private readonly IRunRepository? _repository;

        public RunPersistor(StatsAwareWorkflowRunner runner, IRunRepository? repository = null)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _repository = repository;
        }

        /// <summary>
        /// Executes the given workflow. For now, this is just a thin wrapper
        /// around StatsAwareWorkflowRunner; the repository is kept for future
        /// extension but not yet used.
        /// </summary>
        public async Task<WorkflowResult> ExecuteAsync(
            string workflowName,
            IWorkflow workflow,
            CancellationToken ct = default)
        {
            // In a later step we could create a WorkflowRunArtifact here and
            // persist it via _repository. For now we only delegate.
            return await _runner.ExecuteAsync(workflowName, workflow, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Backwards-compatible overload. Uses a generic workflow name.
        /// Prefer the overload that accepts workflowName explicitly.
        /// </summary>
        public static Task<string> Persist(
            string baseDirectory,
            WorkflowResult result,
            CancellationToken cancellationToken = default)
            => Persist(baseDirectory, "workflow", result, cancellationToken);

        /// <summary>
        /// Persists the markdown report and a JSON run artifact (<c>run.json</c>)
        /// in a timestamped subdirectory and returns the run directory path.
        /// This is intentionally simple and suitable for smoke tests.
        /// </summary>
        public static async Task<string> Persist(
            string baseDirectory,
            string workflowName,
            WorkflowResult result,
            CancellationToken cancellationToken = default)
        {
            if (baseDirectory is null)
                throw new ArgumentNullException(nameof(baseDirectory));

            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("Base directory must not be empty.", nameof(baseDirectory));

            if (string.IsNullOrWhiteSpace(workflowName))
                workflowName = "workflow";

            if (result is null)
                throw new ArgumentNullException(nameof(result));

            // Create a stable run folder name: <workflow>_<timestamp>
            var runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var safeName = SanitizeFileName(workflowName);
            var runDirectory = Path.Combine(baseDirectory, $"{safeName}_{runId}");

            Directory.CreateDirectory(runDirectory);

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            // 1) Markdown report
            var reportPath = Path.Combine(runDirectory, "report.md");
            var markdown = result.ReportMarkdown(workflowName);

            using (var stream = new FileStream(reportPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, encoding))
            {
                await writer.WriteAsync(markdown.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            // 2) JSON run artifact (for compare/automation)
            var now = DateTimeOffset.UtcNow;
            var artifact = new WorkflowRunArtifact(
                RunId: runId,
                WorkflowName: workflowName,
                StartedUtc: now,
                FinishedUtc: now,
                Success: result.Success,
                Metrics: result.Metrics ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                Notes: result.Notes ?? string.Empty
            );

            var json = JsonSerializer.Serialize(
                artifact,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });

            var runJsonPath = Path.Combine(runDirectory, "run.json");
            await File.WriteAllTextAsync(runJsonPath, json, encoding, cancellationToken).ConfigureAwait(false);

            return runDirectory;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);

            foreach (var ch in name)
            {
                builder.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
            }

            return builder.ToString();
        }
    }
}
