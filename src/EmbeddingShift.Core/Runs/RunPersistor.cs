using System.Text;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Core.Stats;

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
            _runner = runner;
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
        /// Persists the markdown report of a <see cref="WorkflowResult"/> in a
        /// timestamped subdirectory and returns the run directory path.
        /// This is intentionally simple and suitable for smoke tests.
        /// </summary>
        public static async Task<string> Persist(
            string baseDirectory,
            WorkflowResult result,
            CancellationToken cancellationToken = default)
        {
            if (baseDirectory is null)
                throw new ArgumentNullException(nameof(baseDirectory));

            if (string.IsNullOrWhiteSpace(baseDirectory))
                throw new ArgumentException("Base directory must not be empty.", nameof(baseDirectory));

            if (result is null)
                throw new ArgumentNullException(nameof(result));

            // Create a stable run folder name: <workflow>_<timestamp>
            var workflowName = result.Workflow();
            if (string.IsNullOrWhiteSpace(workflowName))
                workflowName = "workflow";

            var runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var safeName = SanitizeFileName(workflowName);
            var runDirectory = Path.Combine(baseDirectory, $"{safeName}_{runId}");

            Directory.CreateDirectory(runDirectory);

            var reportPath = Path.Combine(runDirectory, "report.md");
            var markdown = result.ReportMarkdown();

            var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            using (var stream = new FileStream(reportPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, encoding))
            {
                // Use AsMemory so we can pass the cancellation token.
                await writer.WriteAsync(markdown.AsMemory(), cancellationToken)
                            .ConfigureAwait(false);
            }

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
