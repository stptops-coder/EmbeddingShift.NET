using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Mini ingest pipeline (pure simulation, without a dependency on SimpleTextChunker):
    /// - documents (strings)
    /// - simple chunking by length
    /// - simulated embeddings (small float vectors)
    /// </summary>
    public class MiniIngestSimulationPipelineTests
    {
        [Fact]
        public void Documents_are_chunked_and_mapped_to_simulated_embeddings()
        {
            // 1) Mini document corpus
            var docs = new Dictionary<string, string>
            {
                ["policy-1"] =
                    "This is a sample insurance policy. It covers fire and water damage. " +
                    "Exclusions apply for intentional damage and war-related incidents.",
                ["policy-2"] =
                    "Another policy document with slightly different wording. " +
                    "Claims must be reported within 14 days."
            };

            const int maxChunkLength = 80;

            // 2) Pipeline: doc -> chunks -> simulated embeddings
            var allEmbeddings = new List<(string DocId, string ChunkText, float[] Embedding)>();

            foreach (var kvp in docs)
            {
                var docId = kvp.Key;
                var text  = kvp.Value;

                var chunks = ChunkByLength(text, maxChunkLength);
                Assert.NotEmpty(chunks);

                foreach (var chunk in chunks)
                {
                    var embedding = SimulateEmbedding(chunk);
                    allEmbeddings.Add((docId, chunk, embedding));
                }
            }

            // 3) Basic assertions on the pipeline result
            Assert.NotEmpty(allEmbeddings);

            // We expect each embedding to have a fixed vector with 3 dimensions.
            Assert.All(allEmbeddings, e => Assert.Equal(3, e.Embedding.Length));

            // At least one document should have >1 chunk.
            var chunksPerDoc = allEmbeddings
                .GroupBy(e => e.DocId)
                .ToDictionary(g => g.Key, g => g.Count());

            Assert.Contains(chunksPerDoc.Values, c => c > 1);
        }

        private static IEnumerable<string> ChunkByLength(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || maxLen <= 0)
                yield break;

            for (int i = 0; i < text.Length; i += maxLen)
            {
                var len = Math.Min(maxLen, text.Length - i);
                yield return text.Substring(i, len);
            }
        }

        /// <summary>
        /// Very simple "embedding" simulation:
        /// - Dimension 0: chunk length
        /// - Dimension 1: whitespace count
        /// - Dimension 2: whitespace ratio
        /// </summary>
        private static float[] SimulateEmbedding(string text)
        {
            if (text == null)
                return new[] { 0f, 0f, 0f };

            var length = text.Length;
            var ws = text.Count(char.IsWhiteSpace);
            var ratio = length == 0 ? 0f : (float)ws / length;

            return new[]
            {
                (float)length,
                (float)ws,
                ratio
            };
        }
    }
}
