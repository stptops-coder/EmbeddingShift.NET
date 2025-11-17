using System.Linq;
using EmbeddingShift.Preprocessing.Chunking;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Basic tests for SimpleTextChunker to ensure predictable behavior
    /// before using it in larger workflows.
    /// </summary>
    public class SimpleTextChunkerTests
    {
        [Fact]
        public void Short_text_returns_single_chunk()
        {
            var chunker = new SimpleTextChunker();

            var chunks = chunker.Chunk("short text", maxChunkLength: 1000, overlap: 200);

            Assert.Single(chunks);
            Assert.Equal("short text", chunks[0].Content);
        }

        [Fact]
        public void Long_text_is_split_into_multiple_overlapping_chunks()
        {
            var chunker = new SimpleTextChunker();
            var text = string.Join(" ", Enumerable.Repeat("word", 400));

            var chunks = chunker.Chunk(text, maxChunkLength: 120, overlap: 30);

            Assert.True(chunks.Count > 1);

            for (var i = 1; i < chunks.Count; i++)
            {
                var previous = chunks[i - 1];
                var current = chunks[i];

                // Monotonic start positions
                Assert.True(current.Start > previous.Start);

                // There should be an overlap: current start is before previous end
                Assert.True(current.Start < previous.Start + previous.Length);
            }
        }

        [Fact]
        public void Invalid_parameters_throw()
        {
            var chunker = new SimpleTextChunker();

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => chunker.Chunk("text", maxChunkLength: 0, overlap: 0));

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => chunker.Chunk("text", maxChunkLength: 100, overlap: -1));

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => chunker.Chunk("text", maxChunkLength: 100, overlap: 100));
        }
    }
}