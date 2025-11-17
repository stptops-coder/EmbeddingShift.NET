using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Mini Retrieval Simulation:
    /// - documents (strings)
    /// - simple chunking by length
    /// - very simple semantic-ish embeddings
    /// - nearest-neighbour retrieval Ã¼ber Cosine Similarity
    /// </summary>
    public class MiniRetrievalSimulationTests
    {
        [Fact]
        public void Query_about_fire_damage_prefers_policy1()
        {
            var docs = new Dictionary<string, string>
            {
                ["policy-1"] =
                    "This is a sample insurance policy. It covers fire and water damage. " +
                    "Exclusions apply for intentional damage and war-related incidents.",
                ["policy-2"] =
                    "Another policy document with slightly different wording. " +
                    "Claims must be reported within 14 days after the incident."
            };

            const int maxChunkLength = 120;

            // 1) Index aufbauen: Chunks + Embeddings
            var index = new List<(string DocId, string Chunk, float[] Embedding)>();

            foreach (var kvp in docs)
            {
                var docId = kvp.Key;
                var text  = kvp.Value;

                foreach (var chunk in ChunkByLength(text, maxChunkLength))
                {
                    var emb = SemanticEmbedding(chunk);
                    index.Add((docId, chunk, emb));
                }
            }

            Assert.NotEmpty(index);

            // 2) Query -> Embedding
            var query = "fire and water damage to the insured property";
            var queryEmb = SemanticEmbedding(query);

            // 3) Ã„hnlichkeiten berechnen (Cosine) und sortieren
            var ranked = index
                .Select(item => new
                {
                    item.DocId,
                    Score = CosineSimilarity(queryEmb, item.Embedding)
                })
                .GroupBy(x => x.DocId)
                .Select(g => new
                {
                    DocId = g.Key,
                    Score = g.Max(x => x.Score)
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            Assert.True(ranked.Count >= 2);

            var best = ranked[0];
            Assert.Equal("policy-1", best.DocId);
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
        /// Mini-"Semantik": Vektor zÃ¤hlt Keywords:
        /// [0] fire, [1] water, [2] damage, [3] claims
        /// </summary>
        private static float[] SemanticEmbedding(string text)
        {
            text = text?.ToLowerInvariant() ?? string.Empty;

            float fire   = CountOccurrences(text, "fire");
            float water  = CountOccurrences(text, "water");
            float damage = CountOccurrences(text, "damage");
            float claims = CountOccurrences(text, "claims");

            return new[] { fire, water, damage, claims };
        }

        private static float CountOccurrences(string text, string term)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
                return 0f;

            int count = 0;
            int index = 0;

            while ((index = text.IndexOf(term, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += term.Length;
            }

            return count;
        }

        private static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length) throw new ArgumentException("Vector length mismatch.");

            float dot = 0f;
            float na  = 0f;
            float nb  = 0f;

            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na  += a[i] * a[i];
                nb  += b[i] * b[i];
            }

            if (na == 0f || nb == 0f) return 0f;

            return dot / (float)(Math.Sqrt(na) * Math.Sqrt(nb));
        }
    }
}
