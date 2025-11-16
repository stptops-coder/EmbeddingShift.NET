using System;
using System.Collections.Generic;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Core.Shifts;
using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// Central place for predefined pipeline workflows that can be used
    /// from tests, CLI or other callers.
    /// </summary>
    public static class PipelineWorkflows
    {
        /// <summary>
        /// Creates a small evaluation workflow on a synthetic toy dataset.
        /// This is mainly used for debugging and smoke tests.
        /// </summary>
        public static IWorkflow CreateToyEvalWorkflow()
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

            IShift shift = new NoShiftIngestBased();

            var evaluators = new IShiftEvaluator[]
            {
                new CosineMeanEvaluator()
            };

            var logger = new NullRunLogger();
            var runner = new EvaluationRunner(evaluators, logger);
            var inner  = new EvaluationWorkflow(runner);

            return new EvaluationWorkflowAdapter(
                inner,
                shift,
                queries,
                references,
                dataset: "toy-dataset");
        }

        /// <summary>
        /// Simple no-op run logger used for toy workflows.
        /// </summary>
        private sealed class NullRunLogger : IRunLogger
        {
            public Guid StartRun(string kind, string dataset) => Guid.NewGuid();
            public void LogMetric(Guid runId, string metric, double score) { }
            public void CompleteRun(Guid runId, string resultsPath) { }
        }
    }
}
