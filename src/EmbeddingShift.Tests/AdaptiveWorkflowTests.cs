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
    /// End-to-end check:
    /// ShiftTrainingResult → TrainingBackedShiftGenerator → AdaptiveWorkflow.
    ///
    /// Scenario: the stored Delta vector is effectively zero.
    /// In this case the workflow must stay with the fallback shift.
    /// </summary>
    public sealed class AdaptiveWorkflowTests
    {
        [Fact]
        public void Run_UsesFallbackShift_WhenDeltaVectorIsZero()
        {
            // Arrange: repository with a zero delta vector.
            var repo = new InMemoryShiftTrainingResultRepositoryForAdaptive();

            var delta = new float[EmbeddingDimensions.DIM]; // all zeros

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

            // Simple 1D-like geometry in the first dimension:
            // Query = (1, 0, 0, ...)
            // Ref   = (1, 0, 0, ...)
            //
            // Because the delta vector is zero, the generator will only yield
            // the fallback shift. The workflow must therefore return this
            // fallback shift and leave the query unchanged.

            var queryArray = new float[EmbeddingDimensions.DIM];
            queryArray[0] = 1.0f;

            var refArray = new float[EmbeddingDimensions.DIM];
            refArray[0] = 1.0f;

            var query = new ReadOnlyMemory<float>(queryArray);
            var references = new List<ReadOnlyMemory<float>>
            {
                new ReadOnlyMemory<float>(refArray)
            };

            // Act
            var bestShift = workflow.Run(query, references);
            var shifted = bestShift.Apply(queryArray);
            var span = shifted.Span;

            // Assert: adaptive workflow must stay with the fallback shift.
            Assert.IsType<NoShiftIngestBased>(bestShift);

            // The query must remain unchanged in the first dimension.
            Assert.Equal(1.0f, span[0], 3);
        }

        /// <summary>
        /// Minimal in-memory repository for adaptive workflow tests.
        /// </summary>
        private sealed class InMemoryShiftTrainingResultRepositoryForAdaptive : IShiftTrainingResultRepository
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
