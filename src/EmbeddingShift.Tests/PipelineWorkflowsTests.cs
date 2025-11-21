using System.IO;
using System.Threading.Tasks;
using EmbeddingShift.Core.Runs;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Integration test running the toy eval workflow created by
    /// PipelineWorkflows through StatsAwareWorkflowRunner and RunPersistor.
    /// </summary>
    public class PipelineWorkflowsTests
    {
        [Fact]
        public async Task ToyEvalWorkflow_runs_and_is_persisted()
        {
            var workflow = PipelineWorkflows.CreateToyEvalWorkflow();
            Assert.Equal("Evaluation", workflow.Name);

            var runner = new StatsAwareWorkflowRunner();
            var artifacts = await runner.ExecuteAsync("Toy-Pipeline-Test", workflow);

            Assert.True(artifacts.Success);

            var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "PipelineToyRuns");
            var runDir = await RunPersistor.Persist(baseDir, artifacts);

            Assert.True(Directory.Exists(runDir));

            var mdFiles = Directory.GetFiles(runDir, "*.md", SearchOption.AllDirectories);
            Assert.NotEmpty(mdFiles);

            var jsonFiles = Directory.GetFiles(runDir, "*.json", SearchOption.AllDirectories);
            Assert.NotEmpty(jsonFiles);
        }
    }
}
