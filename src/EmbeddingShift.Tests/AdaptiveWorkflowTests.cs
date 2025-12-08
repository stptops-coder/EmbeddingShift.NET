using System;
using System.Collections.Generic;
using System.Linq;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.Adaptive;
using EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Core.Shifts;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that AdaptiveWorkflow + TrainingBackedShiftGenerator
    /// actually use the DeltaVector from the latest ShiftTrainingResult.
    ///
    /// This is an end-to-end check for the adaptive path:
    /// ShiftTrainingResult → TrainingBackedShiftGenerator → AdaptiveWorkflow.
    /// </summary>
    public sealed class AdaptiveWorkflowTests
    {
        [Fact]
        public void Run_UsesLearnedDeltaVector_FromTrainingResult()
        {
            // Arrange: in-memory repository with a simple Delta on first dimension.
            var repo = new InMemoryShiftTrainingResultRepository();

            var delta = new float[EmbeddingDimensions.DIM];
            delta[0] = 2.0f; // +2 on first dimension

            repo.Save(new ShiftTrainingResult
            {
                WorkflowName = "wf",
                DeltaVector = delta
            });

            var fallbackShift = new NoShiftIngestBased();

            var generator = new TrainingBackedShiftGenerator(
                repo,
                workflowName: "wf",
                fallbackShift: fallbackShift);

            var service = new ShiftEvaluationService(
                generator,
                new[] { new CosineSimilarityEvaluator() });

            var workflow = new AdaptiveWorkflow(
                generator,
                service,
                ShiftMethod.Shifted);

            // Simple query and reference: we only care that the chosen shift
            // modifies the query according to DeltaVector.
            var queryArray = new float[EmbeddingDimensions.DIM];
            queryArray[0] = 1.0f;

            var query = new ReadOnlyMemory<float>(queryArray);
            var references = new List<ReadOnlyMemory<float>>
            {
                query
            };

            // Act
            var bestShift = workflow.Run(query, references);
            var shifted = bestShift.Apply(queryArray);
            var span = shifted.Span;

            // Assert: first dimension must be 1 + 2 = 3
            Assert.Equal(3.0f, span[0], 3);
        }

        /// <summary>
        /// Minimal in-memory implementation of IShiftTrainingResultRepository
        /// for testing the adaptive path without touching the file system.
        /// </summary>
        private sealed class InMemoryShiftTrainingResultRepository : IShiftTrainingResultRepository
        {
            private readonly List<ShiftTrainingResult> _results = new();

            public void Save(ShiftTrainingResult result)
            {
                if (result == null) throw new ArgumentNullException(nameof(result));
                _results.Add(result);
            }

            public ShiftTrainingResult? LoadLatest(string workflowName)
            {
                if (string.IsNullOrWhiteSpace(workflowName))
                    throw new ArgumentException("Workflow name must not be null or whitespace.", nameof(workflowName));

                return _results
                    .Where(r => string.Equals(r.WorkflowName, workflowName, StringComparison.Ordinal))
                    .OrderByDescending(r => r.CreatedUtc)
                    .FirstOrDefault();
            }
        }
    }
}
