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
    }
}
