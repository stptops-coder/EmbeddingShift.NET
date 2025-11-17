using System;
using System.IO;
using System.Threading.Tasks;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that TextChunkingWorkflow splits a longer text into multiple chunks.
    /// Uses a temporary directory, does not touch repo files.
    /// </summary>
    public class TextChunkingWorkflowTests
    {
        [Fact]
        public async Task Long_text_is_split_into_multiple_chunks()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "EmbeddingShift_TextChunkingTests", Guid.NewGuid().ToString("N"));
            var tempIn   = Path.Combine(tempRoot, "in");
            var tempOut  = Path.Combine(tempRoot, "out");

            Directory.CreateDirectory(tempIn);

            // Create a single reasonably long text file.
            var longTextPath = Path.Combine(tempIn, "long-sample.txt");
            var longText = new string('A', 1200) + new string('B', 1200); // 2400 chars
            File.WriteAllText(longTextPath, longText);

            var workflow = new TextChunkingWorkflow(
                inputDirectory: tempIn,
                outputDirectory: tempOut,
                maxCharsPerChunk: 500,
                searchPattern: "*.txt",
                recursive: true);

            var runner = new StatsAwareWorkflowRunner();
            var artifacts = await runner.ExecuteAsync("TextChunking-Temp-Test", workflow);

            Assert.True(artifacts.Success);
            Assert.NotNull(artifacts.Metrics);

            Assert.True(artifacts.Metrics.TryGetValue("chunk.totalChunks", out var totalChunks));
            Assert.True(totalChunks >= 3); // 2400 chars / 500 -> at least 5, but 3 is a safe lower bound

            var chunkFiles = Directory.GetFiles(tempOut, "*_chunk*.txt", SearchOption.AllDirectories);
            Assert.True(chunkFiles.Length >= 3);

            // Each chunk file should be non-empty
            foreach (var file in chunkFiles)
            {
                var content = File.ReadAllText(file);
                Assert.False(string.IsNullOrWhiteSpace(content));
            }
        }
    }
}
