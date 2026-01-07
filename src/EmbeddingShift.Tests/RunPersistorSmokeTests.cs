using System.IO;
using System.Threading.Tasks;
using EmbeddingShift.Core.Runs;
using EmbeddingShift.Core.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Runs the SmokeStatsWorkflow via StatsAwareWorkflowRunner and
    /// persists the result with RunPersistor to verify directory +
    /// files are created.
    /// </summary>
    public class RunPersistorSmokeTests
    {
        [Fact]
        public async Task SmokeStatsWorkflow_persists_run_with_report_and_manifest()
        {
            var workflow = new SmokeStatsWorkflow();
            var runner   = new StatsAwareWorkflowRunner();

            var artifacts = await runner.ExecuteAsync("Persist-Test", workflow);

            var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "PersistedRuns");
            var runDir = await RunPersistor.Persist(baseDir, "Persist-Test", artifacts);

            Assert.True(Directory.Exists(runDir));

            var mdFiles = Directory.GetFiles(runDir, "*.md", SearchOption.AllDirectories);
            Assert.NotEmpty(mdFiles);
        }
    }
}
