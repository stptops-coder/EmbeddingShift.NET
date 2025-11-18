using System;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Core.Stats;

namespace EmbeddingShift.Core.Workflows
{
    /// <summary>
    /// Simple helper to execute a single IWorkflow with a stats collector.
    /// Keeps the public surface minimal: you pass a workflow instance and
    /// receive the WorkflowResult with metrics.
    /// </summary>
    public sealed class StatsAwareWorkflowRunner
    {
        private readonly IStatsCollector _stats;

        public StatsAwareWorkflowRunner()
            : this(new InMemoryStatsCollector())
        {
        }

        public StatsAwareWorkflowRunner(IStatsCollector stats)
        {
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
        }

        /// <summary>
        /// Executes the given workflow, wiring it to the internal stats collector
        /// and returning the resulting WorkflowResult (including metrics).
        /// </summary>
        public async Task<WorkflowResult> ExecuteAsync(
            string workflowName,
            IWorkflow workflow,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(workflowName))
                throw new ArgumentException("Workflow name must be provided.", nameof(workflowName));

            if (workflow is null)
                throw new ArgumentNullException(nameof(workflow));

            using (_stats.TrackStep($"Workflow:{workflowName}"))
            {
                return await workflow.RunAsync(_stats, ct).ConfigureAwait(false);
            }
        }
    }
}
