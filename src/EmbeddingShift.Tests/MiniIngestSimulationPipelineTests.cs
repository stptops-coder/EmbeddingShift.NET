using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Mini ingest pipeline (pure simulation, ohne AbhÃ¤ngigkeit von SimpleTextChunker):
    /// - documents (strings)
    /// - simple chunking by length
    /// - simulated embeddings (kleine Float-Vektoren)
    /// </summary>
    public class MiniIngestSimulationPipelineTests
    {
        [Fact]
        public void Documents_are_chunked_and_mapped_to_simulated_embeddings()
        {
            // 1) Mini-Dokumentkorpus
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

            // 2) Pipeline: Doc -> Chunks -> simulierte Embeddings
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

            // 3) Einfache Assertions auf Pipeline-Ergebnis
            Assert.NotEmpty(allEmbeddings);

            // Wir erwarten pro Embedding einen fixen Vektor mit 3 Dimensionen.
            Assert.All(allEmbeddings, e => Assert.Equal(3, e.Embedding.Length));

            // Mindestens ein Dokument sollte >1 Chunk haben.
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
        /// Sehr einfache "Simulation" eines Embeddings:
        /// - Dimension 0: LÃ¤nge des Chunks
        /// - Dimension 1: Anzahl Whitespace-Zeichen
        /// - Dimension 2: Whitespace-Anteil
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
