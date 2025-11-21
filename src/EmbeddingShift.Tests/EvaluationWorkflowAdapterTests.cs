using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Core.Shifts;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Integration-like test: runs EvaluationWorkflow via EvaluationWorkflowAdapter
    /// and StatsAwareWorkflowRunner on a small synthetic dataset.
    /// </summary>
    public class EvaluationWorkflowAdapterTests
    {
        [Fact]
        public async Task EvaluationAdapter_runs_with_stats()
        {
            // Synthetic toy data: 2 queries, 3 references, 3 dimensions.
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

            // Use NoShift as a simple baseline shift.
            IShift shift = new NoShiftIngestBased();

            // Basic evaluator + in-memory logger.
            var evaluators = new IShiftEvaluator[]
            {
                new CosineMeanEvaluator()
            };

            var logger = new InMemoryRunLogger();
            var runner = new EvaluationRunner(evaluators, logger);
            var inner = new EvaluationWorkflow(runner);

            // Adapter exposes EvaluationWorkflow as IWorkflow.
            var adapter = new EvaluationWorkflowAdapter(
                inner,
                shift,
                queries,
                references,
                dataset: "toy-dataset");

            var wfRunner = new StatsAwareWorkflowRunner();

            var artifacts = await wfRunner.ExecuteAsync("Eval-Adapter-Test", adapter);

            Assert.True(artifacts.Success);

            var markdown = artifacts.ReportMarkdown("Evaluation");

            // Header der Auswertung
            Assert.StartsWith("# Evaluation", markdown, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class InMemoryRunLogger : IRunLogger
        {
            public Guid StartRun(string kind, string dataset) => Guid.NewGuid();
            public void LogMetric(Guid runId, string metric, double score) { }
            public void CompleteRun(Guid runId, string resultsPath) { }
        }
    }
}
