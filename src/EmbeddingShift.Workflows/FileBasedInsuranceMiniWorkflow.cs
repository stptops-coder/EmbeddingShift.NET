using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Core.Stats;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.Core.Shifts;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// File-based mini insurance workflow:
    /// - reads policy texts from samples/insurance/policies
    /// - reads queries + relevance from samples/insurance/queries/queries.json
    /// - builds 1536-dimensional keyword-based embeddings via a local provider
    /// - computes MAP@1 and nDCG@3 as metrics
    /// </summary>
    public sealed class FileBasedInsuranceMiniWorkflow : IWorkflow, IPerQueryEvalProvider
    {
        private readonly ILocalEmbeddingProvider _embeddingProvider;
        private readonly IEmbeddingShiftPipeline _shiftPipeline;

        /// <summary>
        /// Per-query evaluation breakdown from the last RunAsync execution.
        /// This is persisted by the pipeline as an extra artifact.
        /// </summary>
        public IReadOnlyList<PerQueryEval> PerQuery { get; private set; } = Array.Empty<PerQueryEval>();


        public FileBasedInsuranceMiniWorkflow()
            : this(
                new KeywordCountEmbeddingProvider(),
                CreateDefaultPipeline())
        {
        }

        public FileBasedInsuranceMiniWorkflow(IEmbeddingShiftPipeline shiftPipeline)
            : this(
                new KeywordCountEmbeddingProvider(),
                shiftPipeline)
        {
        }
        internal FileBasedInsuranceMiniWorkflow(
            ILocalEmbeddingProvider embeddingProvider,
            IEmbeddingShiftPipeline? shiftPipeline = null)
        {
            _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
            _shiftPipeline = shiftPipeline ?? CreateDefaultPipeline();
        }

        public string Name => "FileBased-Insurance-Mini";

        public async Task<WorkflowResult> RunAsync(IStatsCollector stats, CancellationToken ct = default)
        {
            var domainRoot = ResolveDomainRoot();

            var policiesDir = Path.Combine(domainRoot, "policies");
            var queriesPath = Path.Combine(domainRoot, "queries", "queries.json");

            if (!Directory.Exists(policiesDir))
                throw new DirectoryNotFoundException($"Policies directory not found: {policiesDir}");

            if (!File.Exists(queriesPath))
                throw new FileNotFoundException($"Queries file not found: {queriesPath}", queriesPath);

            var docs = Directory
                .EnumerateFiles(policiesDir, "*.txt")
                .OrderBy(p => p)
                .ToDictionary(
                    path => Path.GetFileNameWithoutExtension(path),
                    path => File.ReadAllText(path));

            var json = await File.ReadAllTextAsync(queriesPath, ct).ConfigureAwait(false);
            var queries = JsonSerializer.Deserialize<List<QueryDefinition>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<QueryDefinition>();

            if (queries.Count == 0)
                throw new InvalidOperationException("No queries found in queries.json.");

            var docEmbeddings = docs.ToDictionary(
                kvp => kvp.Key,
                kvp =>
                {
                    var emb = _embeddingProvider.Embed(kvp.Value);
                    _shiftPipeline.ApplyInPlace(emb);
                    return emb;
                });
            var apValues = new List<double>();
            var ndcgValues = new List<double>();
            var perQuery = new List<PerQueryEval>(queries.Count);

            using (stats.TrackStep("FileBased-Insurance-Mini"))
            {
                foreach (var q in queries)
                {
                    ct.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(q.RelevantDocId) || !docs.ContainsKey(q.RelevantDocId))
                    {
                        apValues.Add(0.0);
                        ndcgValues.Add(0.0);

                        perQuery.Add(new PerQueryEval(
                            QueryId: q.Id,
                            RelevantDocId: q.RelevantDocId ?? string.Empty,
                            Rank: 0,
                            Ap1: 0.0,
                            Ndcg3: 0.0,
                            TopDocId: null,
                            TopScore: 0.0));

                        continue;
                    }

                    var qEmb = _embeddingProvider.Embed(q.Text ?? string.Empty);
                    using (QueryShiftContext.Push(q.Id))
                    {
                        _shiftPipeline.ApplyInPlace(qEmb);
                    }
                    var ranked = docEmbeddings
                        .Select(d => new
                        {
                            DocId = d.Key,
                            Score = CosineSimilarity(qEmb, d.Value)
                        })
                        .OrderByDescending(x => x.Score)
                        .ToList();

                    var topHit = ranked.Count > 0 ? ranked[0] : null;
                    var topHit2 = ranked.Count > 1 ? ranked[1] : null;

                    var rankIndex = ranked.FindIndex(r => r.DocId == q.RelevantDocId);
                    if (rankIndex < 0)
                    {
                        apValues.Add(0.0);
                        ndcgValues.Add(0.0);

                        perQuery.Add(new PerQueryEval(
                            QueryId: q.Id,
                            RelevantDocId: q.RelevantDocId,
                            Rank: 0,
                            Ap1: 0.0,
                            Ndcg3: 0.0,
                            TopDocId: topHit?.DocId,
                            TopScore: topHit?.Score ?? 0.0,
                            Top2DocId: topHit2?.DocId,
                            Top2Score: topHit2?.Score ?? 0.0));

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

                    perQuery.Add(new PerQueryEval(
                        QueryId: q.Id,
                        RelevantDocId: q.RelevantDocId,
                        Rank: rank,
                        Ap1: ap,
                        Ndcg3: ndcg,
                        TopDocId: topHit?.DocId,
                        TopScore: topHit?.Score ?? 0.0));
                }
            }

            PerQuery = perQuery;
            var map   = apValues.Count   == 0 ? 0.0 : apValues.Average();
            var ndcg3 = ndcgValues.Count == 0 ? 0.0 : ndcgValues.Average();

            var metrics = new Dictionary<string, double>
            {
                [MetricKeys.Eval.Map1]  = map,
                [MetricKeys.Eval.Ndcg3] = ndcg3
            };

            var notes =
                $"File-based insurance mini workflow over {docs.Count} policies and {queries.Count} queries " +
                "loaded from samples/insurance using a 1536-dimensional embedding simulation.";

            return new WorkflowResult(
                Success: true,
                Metrics: metrics,
                Notes: notes);
        }

        private static string ResolveDomainRoot()
        {
            // Centralized layout: repo-root/samples/insurance
            // (shared with pos/neg training and runner via MiniInsuranceDataset).
            return EmbeddingShift.Workflows.Domains.MiniInsuranceDataset.ResolveDatasetRoot();
        }

        private static double DcgatK(int rankOfRelevant, int k)
        {
            if (rankOfRelevant <= 0 || rankOfRelevant > k)
                return 0.0;

            var denom = Math.Log(rankOfRelevant + 1, 2.0);
            return 1.0 / denom;
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

        private static float CountOccurrences(string text, string term)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
                return 0f;

            text = text.ToLowerInvariant();
            term = term.ToLowerInvariant();

            int count = 0;
            int index = 0;

            while ((index = text.IndexOf(term, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += term.Length;
            }

            return count;
        }

    
        private static IEmbeddingShiftPipeline CreateDefaultPipeline()
        {
            // v1: no-op pipeline (no shifts). Real shifts can be injected later.
            return new EmbeddingShiftPipeline(Array.Empty<IEmbeddingShift>());
        }

        /// <summary>
        /// Pipeline helper for experiments: applies a single domain-level FirstShift
        /// to all insurance embeddings. Used by tests and console experiments to
        /// compare against the baseline pipeline.
        /// </summary>
        public static IEmbeddingShiftPipeline CreateFirstShiftPipeline()
        {
            var firstVector = BuildFirstShiftVector();

            IEmbeddingShift firstShift = new FirstShift(
                name: "Insurance-First",
                shiftVector: firstVector,
                weight: 1.0f);

            return new EmbeddingShiftPipeline(new[] { firstShift });
        }

        /// <summary>
        /// Pipeline helper for experiments: applies both a FirstShift and an
        /// additional DeltaShift on top. This demonstrates the First+Delta
        /// combination in the file-based insurance mini workflow.
        /// </summary>
        public static IEmbeddingShiftPipeline CreateFirstPlusDeltaPipeline()
        {
            var firstVector = BuildFirstShiftVector();
            var deltaVector = BuildDeltaShiftVector();

            IEmbeddingShift firstShift = new FirstShift(
                name: "Insurance-First",
                shiftVector: firstVector,
                weight: 1.0f);

            IEmbeddingShift deltaShift = new DeltaShift(
                name: "Insurance-Delta",
                deltaVector: deltaVector,
                weight: 1.0f);

            return new EmbeddingShiftPipeline(new IEmbeddingShift[] { firstShift, deltaShift });
        }

        /// <summary>
        /// Creates a First+Delta pipeline using a caller-supplied delta vector.
        /// If the vector length does not match the embedding dimension, it will
        /// be truncated or zero-padded to fit the FirstShift vector length.
        /// This overload is the hook for using trained / learned delta candidates.
        /// </summary>
        public static IEmbeddingShiftPipeline CreateFirstPlusDeltaPipeline(float[] deltaVector)
        {
            if (deltaVector == null)
                throw new System.ArgumentNullException(nameof(deltaVector));

            var firstVector = BuildFirstShiftVector();
            var normalizedDelta = new float[firstVector.Length];

            var length = deltaVector.Length < normalizedDelta.Length
                ? deltaVector.Length
                : normalizedDelta.Length;

            for (int i = 0; i < length; i++)
            {
                normalizedDelta[i] = deltaVector[i];
            }

            IEmbeddingShift firstShift = new FirstShift(
                name: "Insurance-First",
                shiftVector: firstVector,
                weight: 1.0f);

            IEmbeddingShift deltaShift = new DeltaShift(
                name: "Insurance-Delta-Learned",
                deltaVector: normalizedDelta,
                weight: 1.0f);

            return new EmbeddingShiftPipeline(new IEmbeddingShift[] { firstShift, deltaShift });
        }


        /// <summary>
        /// Construct the "base" insurance shift vector (FirstShift).
        /// We apply a small, global prior on the core insurance keywords.
        /// Layout is aligned with KeywordCountEmbeddingProvider:
        /// 0: fire, 1: water, 2: damage, 3: theft, 4: claims, 5: flood, 6: storm.
        /// </summary>
        private static float[] BuildFirstShiftVector()
        {
            var provider = new KeywordCountEmbeddingProvider();
            var vector = new float[provider.Dimension];

            // Domain-level prior: globally emphasise damage/theft/claims/flood/storm.
            vector[2] = 0.5f; // damage
            vector[3] = 0.3f; // theft
            vector[4] = 0.3f; // claims
            vector[5] = 0.3f; // flood
            vector[6] = 0.3f; // storm

            return vector;
        }

        /// <summary>
        /// Construct the "delta" adjustment vector (DeltaShift) on top
        /// of the base prior. Here we bias a bit more towards flood/storm.
        /// </summary>
        private static float[] BuildDeltaShiftVector()
        {
            var provider = new KeywordCountEmbeddingProvider();
            var vector = new float[provider.Dimension];

            // Delta adjustment: emphasise flood & storm even more.
            vector[5] = 0.5f; // flood
            vector[6] = 0.5f; // storm

            return vector;
        }

        internal interface ILocalEmbeddingProvider
        {
            int Dimension { get; }
            float[] Embed(string text);
        }

        /// <summary>
        /// Simple 1536-dimensional embedding based on keyword counts in the
        /// first 7 dimensions; the remaining dimensions stay zero. This keeps
        /// cosine geometry equivalent to the earlier 7D demo while matching
        /// the typical size of real embedding models.
        /// </summary>
        private sealed class KeywordCountEmbeddingProvider : ILocalEmbeddingProvider
        {
            public int Dimension => 1536;

            public float[] Embed(string text)
            {
                var vector = new float[Dimension];
                text = text?.ToLowerInvariant() ?? string.Empty;

                vector[0] = CountOccurrences(text, "fire");
                vector[1] = CountOccurrences(text, "water");
                vector[2] = CountOccurrences(text, "damage");
                vector[3] = CountOccurrences(text, "theft");
                vector[4] = CountOccurrences(text, "claims");
                vector[5] = CountOccurrences(text, "flood");
                vector[6] = CountOccurrences(text, "storm");

                return vector;
            }
        }

        private sealed record QueryDefinition(string Id, string Text, string RelevantDocId);
    }
}


