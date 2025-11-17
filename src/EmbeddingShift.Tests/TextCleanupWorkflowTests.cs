using System;
using System.IO;
using System.Threading.Tasks;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that TextCleanupWorkflow can clean a copy of the insurance domain
    /// without touching the repository files.
    /// </summary>
    public class TextCleanupWorkflowTests
    {
        [Fact]
        public async Task Cleanup_over_insurance_copy_produces_cleaned_files()
        {
            var repoRoot     = TestPathHelper.GetRepositoryRoot();
            var sourceDomain = Path.Combine(repoRoot, "data", "domains", "insurance");

            Assert.True(Directory.Exists(sourceDomain), $"Domain directory not found: {sourceDomain}");

            // Work on a temp copy to avoid changing files in the repo.
            var tempRoot  = Path.Combine(Path.GetTempPath(), "EmbeddingShift_TextCleanupTests", Guid.NewGuid().ToString("N"));
            var tempIn    = Path.Combine(tempRoot, "in");
            var tempOut   = Path.Combine(tempRoot, "out");

            CopyDirectory(sourceDomain, tempIn);

            var workflow = new TextCleanupWorkflow(
                inputDirectory: tempIn,
                outputDirectory: tempOut,
                searchPattern: "*.txt",
                recursive: true,
                lowercase: true);

            var runner   = new StatsAwareWorkflowRunner();
            var artifacts = await runner.ExecuteAsync("TextCleanup-Insurance-Copy", workflow);

            Assert.True(artifacts.Success);
            Assert.NotNull(artifacts.Metrics);

            var cleanedFiles = Directory.GetFiles(tempOut, "*.txt", SearchOption.AllDirectories);
            Assert.True(cleanedFiles.Length >= 2);

            // Each cleaned file should be non-empty
            foreach (var file in cleanedFiles)
            {
                var content = File.ReadAllText(file);
                Assert.False(string.IsNullOrWhiteSpace(content));
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var name = Path.GetFileName(file);
                var dest = Path.Combine(destDir, name);
                File.Copy(file, dest, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var name = Path.GetFileName(dir);
                var dest = Path.Combine(destDir, name);
                CopyDirectory(dir, dest);
            }
        }
    }
}
