using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Core.Runs;
using EmbeddingShift.Core.Shifts;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Full chain test:
    /// WorkflowRegistry -> EvaluationWorkflowAdapter -> StatsAwareWorkflowRunner -> RunPersistor.
    /// </summary>
    public class WorkflowRegistryEvalTests
    {
        [Fact]
        public async Task Registry_can_run_eval_workflow_and_persist_results()
        {
            // Synthetic toy data (2 queries, 3 references, 3 dimensions)
            var queries = new List<ReadOnlyMemory<float>>
            {
                new float[] { 1f, 0f, 0f },
                new float[] { 0f, 1f, 0f }
            };

            var references = new List<ReadOnlyMemory<float>>
            {
                new float[] { 1f, 0f, 0f },
                new float[] { 0f, 1f, 0f },
                new float[] { 0f, 0f, 1f }
            };

            IShift shift = new NoShiftIngestBased();

            var evaluators = new IShiftEvaluator[]
            {
                new CosineMeanEvaluator()
            };

            var logger = new InMemoryRunLogger();
            var runner = new EvaluationRunner(evaluators, logger);
            var inner  = new EvaluationWorkflow(runner);

            var registry = new WorkflowRegistry()
                .Register("eval-toy", () =>
                    new EvaluationWorkflowAdapter(
                        inner,
                        shift,
                        queries,
                        references,
                        dataset: "toy-dataset"));

            // Resolve via registry
            var wf = registry.Resolve("eval-toy");
            Assert.Equal("Evaluation", wf.Name);

            var wfRunner = new StatsAwareWorkflowRunner();
            var artifacts = await wfRunner.ExecuteAsync("Eval-Registry-Test", wf);

            Assert.True(artifacts.Success);

            var markdown = artifacts.ReportMarkdown("Evaluation");
            Assert.StartsWith("# Evaluation", markdown, StringComparison.OrdinalIgnoreCase);

            var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "RegistryPersistedRuns");
            var runDir = await RunPersistor.Persist(baseDir, artifacts);

            Assert.True(Directory.Exists(runDir));

            var mdFiles = Directory.GetFiles(runDir, "*.md", SearchOption.AllDirectories);
            Assert.NotEmpty(mdFiles);
        }

        private sealed class InMemoryRunLogger : IRunLogger
        {
            public Guid StartRun(string kind, string dataset) => Guid.NewGuid();
            public void LogMetric(Guid runId, string metric, double score) { }
            public void CompleteRun(Guid runId, string resultsPath) { }
        }
    }
}
