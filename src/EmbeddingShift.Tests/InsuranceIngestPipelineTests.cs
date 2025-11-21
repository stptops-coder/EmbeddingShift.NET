using System.IO;
using System.Threading.Tasks;
using EmbeddingShift.Core.Runs;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Full insurance ingest pipeline:
    /// TextFileIngestWorkflow -> StatsAwareWorkflowRunner -> RunPersistor.
    /// </summary>
    public class InsuranceIngestPipelineTests
    {
        [Fact]
        public async Task Ingest_insurance_domain_is_persisted_as_run()
        {
            // Use the same repo root pattern as other tests.
            var repoRoot = TestPathHelper.GetRepositoryRoot();
            var domainDir = Path.Combine(repoRoot, "data", "domains", "insurance");

            Assert.True(Directory.Exists(domainDir), $"Domain directory not found: {domainDir}");

            var workflow = new TextFileIngestWorkflow(
                inputDirectory: domainDir,
                searchPattern: "*.txt",
                recursive: true);

            var runner = new StatsAwareWorkflowRunner();
            var artifacts = await runner.ExecuteAsync("Ingest-Insurance-Persist-Test", workflow);

            Assert.True(artifacts.Success);

            // Persist the run to a domain-specific folder.
            var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "PersistedRuns", "insurance_ingest");
            var runDir = await RunPersistor.Persist(baseDir, artifacts);

            Assert.True(Directory.Exists(runDir));

            // Es sollten mindestens ein Markdown-Report und mindestens
            // eine JSON-Datei (Manifest/Run-Info) im Run-Verzeichnis liegen.
            var mdFiles = Directory.GetFiles(runDir, "*.md", SearchOption.AllDirectories);
            Assert.NotEmpty(mdFiles);

            var jsonFiles = Directory.GetFiles(runDir, "*.json", SearchOption.AllDirectories);
            Assert.NotEmpty(jsonFiles);
        }
    }
}
