using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// Provides preconfigured workflow registries that can be used
    /// from CLI, tests or other callers.
    /// </summary>
    public static class RegistryDefaults
    {
        /// <summary>
        /// Creates a registry with simple toy workflows.
        /// </summary>
        public static WorkflowRegistry CreateWithToyWorkflows()
        {
            var registry = new WorkflowRegistry();

            // SmokeStats demo workflow
            registry.Register("smoke-stats", () => new SmokeStatsWorkflow());

            // Toy evaluation workflow based on synthetic vectors
            registry.Register("toy-eval", PipelineWorkflows.CreateToyEvalWorkflow);

            return registry;
        }
    }
}
