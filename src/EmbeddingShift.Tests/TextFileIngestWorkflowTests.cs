using System;
using System.IO;
using System.Threading.Tasks;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Runs TextFileIngestWorkflow against the insurance domain folder
    /// and verifies that some basic ingest metrics are produced.
    /// </summary>
    public class TextFileIngestWorkflowTests
    {
        [Fact]
        public async Task Ingest_over_insurance_domain_produces_basic_metrics()
        {
            var domainDir = TestPathHelper.GetInsuranceDomainDirectory();

            Assert.True(Directory.Exists(domainDir), $"Domain directory not found: {domainDir}");

            var workflow = new TextFileIngestWorkflow(
                inputDirectory: domainDir,
                searchPattern: "*.txt",
                recursive: true);

            var runner = new StatsAwareWorkflowRunner();
            var artifacts = await runner.ExecuteAsync("Ingest-Insurance-Test", workflow);

            Assert.True(artifacts.Success);

            Assert.NotNull(artifacts.Metrics);
            Assert.True(artifacts.Metrics.TryGetValue("ingest.files", out var fileCount));
            Assert.True(artifacts.Metrics.TryGetValue("ingest.totalLines", out var totalLines));

            Assert.True(fileCount >= 2);
            Assert.True(totalLines >= 2);
        }
    }
}
