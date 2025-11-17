using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Mini-Metrik-Layer auf Retrieval:
    /// - zwei Policies
    /// - zwei Queries
    /// - Precision@1 und Mean Precision Ã¼ber beide Queries
    /// </summary>
    public class MiniRetrievalMetricsTests
    {
        [Fact]
        public void Two_query_scenario_has_mean_precision_at_1_equal_1()
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

            // Dokument-Embeddings (sehr einfache Keyword-ZÃ¤hlung)
            var docEmbeddings = docs.ToDictionary(
                kvp => kvp.Key,
                kvp => SemanticEmbedding(kvp.Value));

            var queries = new[]
            {
                new { Id = "q1", Text = "fire and water damage to the insured property", RelevantDoc = "policy-1" },
                new { Id = "q2", Text = "deadline for reporting claims", RelevantDoc = "policy-2" }
            };

            var precisionsAt1 = new List<double>();

            foreach (var q in queries)
            {
                var qEmb = SemanticEmbedding(q.Text);

                var ranked = docEmbeddings
                    .Select(d => new
                    {
                        DocId = d.Key,
                        Score = CosineSimilarity(qEmb, d.Value)
                    })
                    .OrderByDescending(x => x.Score)
                    .ToList();

                Assert.True(ranked.Count >= 2);

                var top1 = ranked[0].DocId;
                var precisionAt1 = top1 == q.RelevantDoc ? 1.0 : 0.0;
                precisionsAt1.Add(precisionAt1);
            }

            var meanPrecisionAt1 = precisionsAt1.Average();

            // Beide Queries treffen das richtige Dokument an erster Stelle.
            Assert.Equal(1.0, meanPrecisionAt1, precision: 3);
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
