using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.ConsoleEval;
using EmbeddingShift.Core.Infrastructure;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that the MiniInsurancePosNegRunner can run end-to-end against
    /// the simulated embedding backend and creates a metrics-posneg.json/md
    /// pair in the results directory.
    ///
    /// This is an integration-style test that protects the basic contract of
    /// the pos-neg evaluation command.
    /// </summary>
    public class MiniInsurancePosNegRunnerTests
    {
        [Fact]
        public async Task RunAsync_with_sim_backend_creates_metrics_files()
        {
            // Arrange: resolve the same results root that the runner uses.
            var root = DirectoryLayout.ResolveResultsRoot("insurance");
            Directory.CreateDirectory(root);

            var pattern = "mini-insurance-posneg-run_*";
            var before = Directory.GetDirectories(root, pattern, SearchOption.TopDirectoryOnly);

            // Act
            await MiniInsurancePosNegRunner.RunAsync(EmbeddingBackend.Sim);

            // Assert: there should be at least one (new) run directory
            var after = Directory.GetDirectories(root, pattern, SearchOption.TopDirectoryOnly);
            Assert.NotEmpty(after);

            var newDirs = after.Except(before).ToArray();
            var targetDir = (newDirs.Length > 0 ? newDirs : after)
                .OrderBy(d => d)
                .Last();

            Assert.True(File.Exists(Path.Combine(targetDir, "metrics-posneg.json")));
            Assert.True(File.Exists(Path.Combine(targetDir, "metrics-posneg.md")));
        }
    }
}
