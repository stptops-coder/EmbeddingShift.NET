using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Core.Stats;
using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// Small self-contained semantic retrieval workflow:
    /// - 3 insurance-like policies
    /// - 3 queries
    /// - keyword-based embeddings
    /// - produces map@1 and ndcg@3 as metrics
    /// </summary>
    public sealed class MiniSemanticRetrievalWorkflow : IWorkflow
    {
        public string Name => "Mini-Semantic-Retrieval";

        public Task<WorkflowResult> RunAsync(IStatsCollector stats, CancellationToken ct = default)
        {
            var docs = new Dictionary<string, string>
            {
                ["policy-fire-water"] =
                    "This insurance policy covers fire and water damage to the insured property.",
                ["policy-theft-claims"] =
                    "This policy covers theft of personal belongings. Claims must be reported quickly.",
                ["policy-flood-storm"] =
                    "This policy covers flood and storm damage after heavy rain or storms."
            };

            var docEmbeddings = docs.ToDictionary(
                kvp => kvp.Key,
                kvp => SemanticEmbedding(kvp.Value));

            var queries = new[]
            {
                new { Id = "q1", Text = "fire and water damage to the house",      RelevantDoc = "policy-fire-water"   },
                new { Id = "q2", Text = "theft of personal belongings and claims", RelevantDoc = "policy-theft-claims" },
                new { Id = "q3", Text = "flood and storm damage after heavy rain", RelevantDoc = "policy-flood-storm"  }
            };

            var apValues   = new List<double>();
            var ndcgValues = new List<double>();

            using (stats.TrackStep("Mini-EndToEnd-Ranking"))
            {
                foreach (var q in queries)
                {
                    ct.ThrowIfCancellationRequested();

                    var qEmb = SemanticEmbedding(q.Text);

                    var ranked = docEmbeddings
                        .Select(d => new
                        {
                            DocId = d.Key,
                            Score = CosineSimilarity(qEmb, d.Value)
                        })
                        .OrderByDescending(x => x.Score)
                        .ToList();

                    var rankIndex = ranked.FindIndex(r => r.DocId == q.RelevantDoc);
                    if (rankIndex < 0)
                    {
                        apValues.Add(0.0);
                        ndcgValues.Add(0.0);
                        continue;
                    }

                    var rank = rankIndex + 1;
                    var ap   = 1.0 / rank;
                    apValues.Add(ap);

                    const int k = 3;
                    var dcg  = DcgatK(rank, k);
                    var idcg = DcgatK(1, k);
                    var ndcg = idcg == 0.0 ? 0.0 : dcg / idcg;
                    ndcgValues.Add(ndcg);
                }
            }

            var meanAp   = apValues.Count == 0 ? 0.0 : apValues.Average();
            var meanNdcg = ndcgValues.Count == 0 ? 0.0 : ndcgValues.Average();

            var metrics = new Dictionary<string, double>
            {
                ["map@1"]  = meanAp,
                ["ndcg@3"] = meanNdcg
            };

            var notes = "Mini end-to-end ranking scenario with 3 queries and 3 policies (Workflows layer).";

            return Task.FromResult(new WorkflowResult(
                Success: true,
                Metrics: metrics,
                Notes: notes));
        }

        /// <summary>
        /// Mini "semantics": simple keyword counts in a fixed order:
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

            var denom = Math.Log(rankOfRelevant + 1, 2.0);
            return 1.0 / denom;
        }
    }
}
