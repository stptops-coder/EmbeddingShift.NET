using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Mini end-to-end scenario:
    /// - 3 insurance policies
    /// - 3 queries
    /// - keyword-count "embeddings"
    /// - cosine retrieval
    /// - MAP and nDCG@3 across all queries
    /// </summary>
    public class MiniEndToEndRankingMetricsTests
    {
        [Fact]
        public void Three_query_scenario_has_perfect_map_and_ndcg()
        {
            // 1) Documents (policies)
            var docs = new Dictionary<string, string>
            {
                ["policy-fire-water"] =
                    "This insurance policy covers fire and water damage to the insured property.",
                ["policy-theft-claims"] =
                    "This policy covers theft of personal belongings. Claims must be reported quickly.",
                ["policy-flood-storm"] =
                    "This policy covers flood and storm damage after heavy rain or storms."
            };

            // 2) Document embeddings (simple keyword counts)
            var docEmbeddings = docs.ToDictionary(
                kvp => kvp.Key,
                kvp => SemanticEmbedding(kvp.Value));

            // AP for a single relevant answer = 1 / rank
            var queries = new[]
            {
                new { Id = "q1", Text = "fire and water damage to the house",      RelevantDoc = "policy-fire-water"   },
                new { Id = "q2", Text = "theft of personal belongings and claims", RelevantDoc = "policy-theft-claims" },
                new { Id = "q3", Text = "flood and storm damage after heavy rain", RelevantDoc = "policy-flood-storm"  }
            };

            var apValues   = new List<double>();
            var ndcgValues = new List<double>();

            foreach (var q in queries)
            {
                var qEmb = SemanticEmbedding(q.Text);

                // Ranking via Cosine Similarity
                var ranked = docEmbeddings
                    .Select(d => new
                    {
                        DocId = d.Key,
                        Score = CosineSimilarity(qEmb, d.Value)
                    })
                    .OrderByDescending(x => x.Score)
                    .ToList();

                Assert.True(ranked.Count == docs.Count);

                // Find the rank of the relevant document (1-based).
                var rankIndex = ranked.FindIndex(r => r.DocId == q.RelevantDoc);
                Assert.True(rankIndex >= 0);

                var rank = rankIndex + 1;

                // AP for a single relevant answer = 1 / rank
                var ap = 1.0 / rank;
                apValues.Add(ap);

                // nDCG@3: binary relevance only (1 for relevant doc, 0 otherwise)
                var k = 3;
                var dcg = DcgatK(rank, k);
                var idcg = DcgatK(1, k); // Ideal ranking: relevant doc at position 1
                var ndcg = idcg == 0.0 ? 0.0 : dcg / idcg;

                ndcgValues.Add(ndcg);
            }

            var meanAp   = apValues.Average();
            var meanNdcg = ndcgValues.Average();

            // In this constructed scenario, the relevant document is always ranked #1:
            // -> MAP = 1.0, nDCG@3 = 1.0
            Assert.Equal(1.0, meanAp,   precision: 3);
            Assert.Equal(1.0, meanNdcg, precision: 3);
        }

        /// <summary>
        /// Toy semantics: vector counts keywords:
        /// [0] fire, [1] water, [2] damage, [3] theft, [4] claims, [5] flood, [6] storm
        /// </summary>
        private static float[] SemanticEmbedding(string text)
        {
            text = text?.ToLowerInvariant() ?? string.Empty;

            float fire   = CountOccurrences(text, "fire");
            float water  = CountOccurrences(text, "water");
            float damage = CountOccurrences(text, "damage");
            float theft  = CountOccurrences(text, "theft");
            float claims = CountOccurrences(text, "claims");
            float flood  = CountOccurrences(text, "flood");
            float storm  = CountOccurrences(text, "storm");

            return new[] { fire, water, damage, theft, claims, flood, storm };
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

        private static double DcgatK(int rankOfRelevant, int k)
        {
            if (rankOfRelevant <= 0 || rankOfRelevant > k)
                return 0.0;

            // Binary relevance: relevant hit at position "rankOfRelevant" with gain 1.
            // DCG formula: rel / log2(rank+1); rel = 1
            var denom = Math.Log(rankOfRelevant + 1, 2.0);
            return 1.0 / denom;
        }
    }
}
