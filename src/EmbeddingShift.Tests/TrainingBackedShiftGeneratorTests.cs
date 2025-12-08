using System;
using System.Collections.Generic;
using System.Linq;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.Adaptive;
using EmbeddingShift.Core.Shifts;
using Xunit;

namespace EmbeddingShift.Tests
{
    public sealed class TrainingBackedShiftGeneratorTests
    {
        [Fact]
        public void Generate_UsesDeltaVector_FromLatestTrainingResult()
        {
            // Arrange
            var repo = new InMemoryShiftTrainingResultRepository();

            var delta = new float[EmbeddingDimensions.DIM];
            delta[0] = 1.0f;
            delta[1] = -2.0f;
            delta[2] = 0.5f;

            repo.Save(new ShiftTrainingResult
            {
                WorkflowName = "wf",
                DeltaVector = delta
            });

            var fallback = new NoShiftIngestBased();
            var generator = new TrainingBackedShiftGenerator(repo, "wf", fallback);

            var input = new float[EmbeddingDimensions.DIM];
            input[0] = 10.0f;
            input[1] = 10.0f;
            input[2] = 10.0f;

            var pairs = new List<(ReadOnlyMemory<float> Query, ReadOnlyMemory<float> Answer)>
            {
                (input, input)
            };

            // Act
            var shifts = generator.Generate(pairs).ToList();

            // Assert
            Assert.Single(shifts);
            var shift = shifts[0];

            var output = shift.Apply(input);
            var span = output.Span;

            Assert.Equal(11.0f, span[0], 3);   // 10 + 1
            Assert.Equal(8.0f, span[1], 3);   // 10 - 2
            Assert.Equal(10.5f, span[2], 3);   // 10 + 0.5
        }

        [Fact]
        public void Generate_FallsBackToNoShift_WhenNoTrainingResult()
        {
            // Arrange
            var repo = new InMemoryShiftTrainingResultRepository();
            var fallback = new NoShiftIngestBased();
            var generator = new TrainingBackedShiftGenerator(repo, "wf", fallback);

            var input = new float[EmbeddingDimensions.DIM];
            input[0] = 5.0f;

            var pairs = new List<(ReadOnlyMemory<float> Query, ReadOnlyMemory<float> Answer)>
            {
                (input, input)
            };

            // Act
            var shifts = generator.Generate(pairs).ToList();

            // Assert
            Assert.Single(shifts);
            var shift = shifts[0];

            var output = shift.Apply(input);
            var span = output.Span;

            // NoShift.IngestBased should just copy the input.
            Assert.Equal(5.0f, span[0], 3);
        }

        private sealed class InMemoryShiftTrainingResultRepository : IShiftTrainingResultRepository
        {
            private ShiftTrainingResult? _result;

            public void Save(ShiftTrainingResult result)
            {
                _result = result;
            }

            public ShiftTrainingResult? LoadLatest(string workflowName)
            {
                if (_result == null)
                    return null;

                if (!string.Equals(_result.WorkflowName, workflowName, StringComparison.Ordinal))
                    return null;

                return _result;
            }
        }
    }
}
