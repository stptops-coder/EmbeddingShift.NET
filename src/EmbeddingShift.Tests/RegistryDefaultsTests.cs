using System.Threading.Tasks;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that the default registry can resolve and run the toy workflows.
    /// </summary>
    public class RegistryDefaultsTests
    {
        [Fact]
        public async Task Default_registry_runs_smoke_and_toy_eval()
        {
            var registry = RegistryDefaults.CreateWithToyWorkflows();
            var runner   = new StatsAwareWorkflowRunner();

            var smoke = registry.Resolve("smoke-stats");
            var smokeArtifacts = await runner.ExecuteAsync("Registry-Smoke", smoke);
            Assert.True(smokeArtifacts.Success);

            var eval = registry.Resolve("toy-eval");
            var evalArtifacts = await runner.ExecuteAsync("Registry-ToyEval", eval);
            Assert.True(evalArtifacts.Success);
        }
    }
}
