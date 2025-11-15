using System.IO;
using System.Threading.Tasks;
using EmbeddingShift.Core.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Simple integration test that runs the SmokeStatsWorkflow via
    /// StatsAwareWorkflowRunner and writes a markdown report so you
    /// can inspect the stats output.
    /// </summary>
    public class StatsSmokeWorkflowTests
    {
        [Fact]
        public async Task SmokeStatsWorkflow_runs_and_produces_report()
        {
            var workflow = new SmokeStatsWorkflow();
            var runner = new StatsAwareWorkflowRunner();

            var artifacts = await runner.ExecuteAsync("Smoke-Test", workflow);

            Assert.True(artifacts.Success);

            var reportDir = Path.Combine(Directory.GetCurrentDirectory(), "TestRuns");
            Directory.CreateDirectory(reportDir);
            var reportPath = Path.Combine(reportDir, "Smoke_RunReport.md");

            File.WriteAllText(reportPath, artifacts.ReportMarkdown);
        }
    }
}
