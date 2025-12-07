using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.ConsoleEval;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that the MiniInsurancePosNegTrainer can run end-to-end
    /// against the simulated embedding backend and produces a non-empty
    /// Delta vector with the expected dimensionality.
    ///
    /// This protects the basic contract of TrainAsync (no exceptions,
    /// correct workflow name, at least one comparison run, correct
    /// embedding dimension).
    /// </summary>
    public class MiniInsurancePosNegTrainerTests
    {
        [Fact]
        public async Task TrainAsync_with_sim_backend_produces_nonempty_delta_vector()
        {
            // Act
            var result = await MiniInsurancePosNegTrainer.TrainAsync(EmbeddingBackend.Sim);

            // Assert basic shape of the training result.
            Assert.NotNull(result);
            Assert.Equal("mini-insurance-posneg", result.WorkflowName);
            Assert.True(result.ComparisonRuns > 0);

            Assert.NotNull(result.DeltaVector);
            var vector = result.DeltaVector!;

            // Mini-Insurance uses 1536-dimensional embeddings in the simulation.
            Assert.Equal(1536, vector.Length);
        }
    }
}
