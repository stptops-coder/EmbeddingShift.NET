using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.ConsoleEval.Repositories;
using EmbeddingShift.ConsoleEval.MiniInsurance;
using EmbeddingShift.Core.Training;
using EmbeddingShift.Core.Training.CancelOut;
using EmbeddingShift.Core.Training.PosNeg;
using EmbeddingShift.Core.Infrastructure;

namespace EmbeddingShift.ConsoleEval
{
    /// <summary>
    /// Trainer that learns a single global delta vector from positive/negative
    /// document pairs for the mini-insurance sample domain.
    ///
    /// It uses the generic IEmbeddingProvider (via EmbeddingBackend) and
    /// persists the result as a ShiftTrainingResult so that it can be re-used
    /// across domains and scopes.
    /// </summary>
    public static class MiniInsurancePosNegTrainer
    {
        private const string WorkflowName = "mini-insurance-posneg";

        public static Task<ShiftTrainingResult> TrainAsync(EmbeddingBackend backend)
        {
            // Backwards-compatible default behavior.
            return TrainAsync(
                backend,
                mode: TrainingMode.Production,
                cancelOutEpsilon: 1e-3f);
        }

        public static async Task<ShiftTrainingResult> TrainAsync(
            EmbeddingBackend backend,
            TrainingMode mode,
            float cancelOutEpsilon,
            int hardNegTopK = 1)

        {
            var provider = EmbeddingProviderFactory.Create(backend);

            var domainRoot = ResolveDomainRoot();
            var policiesDir = Path.Combine(domainRoot, "policies");
            var queriesPath = Path.Combine(domainRoot, "queries", "queries.json");

            if (!Directory.Exists(policiesDir))
                throw new DirectoryNotFoundException($"Policies directory not found: {policiesDir}");

            if (!File.Exists(queriesPath))
                throw new FileNotFoundException($"Queries file not found: {queriesPath}");

            var docs = Directory
                .EnumerateFiles(policiesDir, "*.txt")
                .OrderBy(p => p)
                .ToDictionary(
                    path => Path.GetFileNameWithoutExtension(path),
                    path => File.ReadAllText(path));

            if (docs.Count == 0)
                throw new InvalidOperationException("No policy documents found in the policies directory.");

            var json = await File.ReadAllTextAsync(queriesPath).ConfigureAwait(false);
            var queries = JsonSerializer.Deserialize<List<QueryDefinition>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<QueryDefinition>();

            if (queries.Count == 0)
                throw new InvalidOperationException("No queries found in queries.json.");

            // Embed all documents once.
            var docEmbeddings = new Dictionary<string, float[]>(docs.Count, StringComparer.OrdinalIgnoreCase);
            float[]? firstEmbedding = null;

            foreach (var kvp in docs)
            {
                var emb = await provider.GetEmbeddingAsync(kvp.Value).ConfigureAwait(false);
                if (emb == null)
                    throw new InvalidOperationException("Embedding provider returned a null embedding.");

                if (firstEmbedding == null)
                {
                    firstEmbedding = emb;
                }
                else if (emb.Length != firstEmbedding.Length)
                {
                    throw new InvalidOperationException("Embedding provider returned embeddings with inconsistent dimensions.");
                }

                docEmbeddings[kvp.Key] = emb;
            }

            if (firstEmbedding == null)
                throw new InvalidOperationException("No embeddings were created for the policy documents.");

            var dim = firstEmbedding.Length;

            // Diagnostics via env flags (off by default).
            var debug = IsEnvEnabled("EMBEDDINGSHIFT_POSNEG_DEBUG");
            var disableNormClip =
                IsEnvEnabled("EMBEDDINGSHIFT_POSNEG_NOCLIP") ||
                IsEnvEnabled("EMBEDDINGSHIFT_POSNEG_DISABLE_CLIP"); // alias for convenience

            const float MaxL2Norm = 1.5f;

            Action<string>? debugLog = debug ? line => Console.WriteLine(line) : null;

            var learnerQueries = queries
                .Where(q =>
                    !string.IsNullOrWhiteSpace(q.RelevantDocId) &&
                    docEmbeddings.ContainsKey(q.RelevantDocId))
                .Select(q => new PosNegTrainingQuery(q.Id, q.Text, q.RelevantDocId))
                .ToList();
            var options = new PosNegLearningOptions(
                MaxL2Norm: MaxL2Norm,
                DisableNormClip: disableNormClip,
                Debug: debug,
                HardNegTopK: hardNegTopK);

            var learn = await PosNegDeltaVectorLearner
                .LearnAsync(provider, learnerQueries, docEmbeddings, options, debugLog)
                .ConfigureAwait(false);

            var deltaVector = learn.DeltaVector;
            var stats = learn.Stats;

            var cancel = CancelOutEvaluator.Evaluate(deltaVector, cancelOutEpsilon);
            if (cancel.IsCancelled)
            {
                Console.WriteLine($"[PosNeg] Cancel-out gate triggered: {cancel.Reason}");
            }

            Console.WriteLine(
                $"[PosNeg] Summary: cases={stats.Cases}, uniquePairs={stats.UniquePairs}, avg|dir|={stats.AvgDirectionNorm:0.000000}, min|dir|={stats.MinDirectionNorm:0.000000}, max|dir|={stats.MaxDirectionNorm:0.000000}, zeroDirs={stats.ZeroDirections}");

            var clipState = options.DisableNormClip ? "disabled" : (stats.NormClipApplied ? "applied" : "none");
            Console.WriteLine(
                $"[PosNeg] Learned delta: |Δ|={stats.PostClipDeltaNorm:0.000000} (preClip={stats.PreClipDeltaNorm:0.000000}, clip={clipState}, max={options.MaxL2Norm:0.0})");

            if (stats.CancelOutSuspected)
            {
                Console.WriteLine("[PosNeg] Note: delta nearly zero despite strong per-case directions (possible cancel-out).");
            }

            if (!debug && stats.PostClipDeltaNorm < 1e-6f)
            {
                Console.WriteLine("[PosNeg] Hint: set EMBEDDINGSHIFT_POSNEG_DEBUG=1 to print per-case details.");
            }

            var selection = await EvaluateSelectionMetricsAsync(
                    provider,
                    queries,
                    docEmbeddings,
                    deltaVector,
                    dim)
                .ConfigureAwait(false);

            Console.WriteLine(
                $"[PosNeg] Selection metrics: MAP@1 {selection.MapBaseline:0.000} -> {selection.MapShifted:0.000} ({selection.DeltaMap:+0.000;-0.000;0.000}), " +
                $"NDCG@3 {selection.Ndcg3Baseline:0.000} -> {selection.Ndcg3Shifted:0.000} ({selection.DeltaNdcg3:+0.000;-0.000;0.000}), " +
                $"score={selection.SelectionScore:+0.000;-0.000;0.000}");

            var resultsRoot = MiniInsurancePaths.GetDomainRoot();

            var trainingResult = new ShiftTrainingResult
            {
                WorkflowName = WorkflowName,
                CreatedUtc = DateTime.UtcNow,
                BaseDirectory = resultsRoot,
                ComparisonRuns = stats.Cases,
                ImprovementFirst = 0.0,
                ImprovementFirstPlusDelta = 0.0,
                DeltaImprovement = 0.0,
                DeltaVector = learn.DeltaVector,
                TrainingMode = mode.ToString(),
                CancelOutEpsilon = cancelOutEpsilon,
                IsCancelled = cancel.IsCancelled,
                CancelReason = cancel.Reason,
                DeltaNorm = cancel.DeltaNorm,
                SelectionMapAt1Baseline = selection.MapBaseline,
                SelectionMapAt1Shifted = selection.MapShifted,
                SelectionNdcg3Baseline = selection.Ndcg3Baseline,
                SelectionNdcg3Shifted = selection.Ndcg3Shifted,
                SelectionScore = selection.SelectionScore,
                ScopeId = MiniInsuranceScopes.DefaultScopeId
            };

            var repository = new FileSystemShiftTrainingResultRepository(resultsRoot);
            repository.Save(trainingResult);

            return trainingResult;
        }

        private static async Task<SelectionMetrics> EvaluateSelectionMetricsAsync(
            IEmbeddingProvider provider,
            IReadOnlyList<QueryDefinition> queries,
            IReadOnlyDictionary<string, float[]> docEmbeddings,
            float[] shift,
            int dim)
        {
            var apBaseline = new List<double>();
            var ndcgBaseline = new List<double>();
            var apShifted = new List<double>();
            var ndcgShifted = new List<double>();

            foreach (var q in queries)
            {
                if (string.IsNullOrWhiteSpace(q.RelevantDocId) ||
                    !docEmbeddings.ContainsKey(q.RelevantDocId))
                {
                    continue;
                }

                var qEmb = await provider.GetEmbeddingAsync(q.Text ?? string.Empty).ConfigureAwait(false);
                if (qEmb is null || qEmb.Length != dim)
                    continue;

                var rankedBaseline = docEmbeddings
                    .Select(d => new
                    {
                        DocId = d.Key,
                        Score = CosineSimilarity(qEmb, d.Value)
                    })
                    .OrderByDescending(x => x.Score)
                    .ToList();

                var rankBaseline = FindRank(rankedBaseline, q.RelevantDocId);
                if (rankBaseline <= 0)
                    continue;

                var qShifted = new float[dim];
                for (var i = 0; i < dim; i++)
                    qShifted[i] = qEmb[i] + shift[i];

                var rankedShifted = docEmbeddings
                    .Select(d => new
                    {
                        DocId = d.Key,
                        Score = CosineSimilarity(qShifted, d.Value)
                    })
                    .OrderByDescending(x => x.Score)
                    .ToList();

                var rankShifted = FindRank(rankedShifted, q.RelevantDocId);
                if (rankShifted <= 0)
                    continue;

                apBaseline.Add(1.0 / rankBaseline);
                apShifted.Add(1.0 / rankShifted);

                const int k = 3;
                var dcgBaseline = DcgatK(rankBaseline, k);
                var dcgShifted = DcgatK(rankShifted, k);
                var idcg = DcgatK(1, k);

                var ndcgB = idcg == 0.0 ? 0.0 : dcgBaseline / idcg;
                var ndcgS = idcg == 0.0 ? 0.0 : dcgShifted / idcg;

                ndcgBaseline.Add(ndcgB);
                ndcgShifted.Add(ndcgS);
            }

            var usedCases = apBaseline.Count;
            var mapBaseline = usedCases == 0 ? 0.0 : apBaseline.Average();
            var mapShifted = usedCases == 0 ? 0.0 : apShifted.Average();
            var ndcg3Baseline = usedCases == 0 ? 0.0 : ndcgBaseline.Average();
            var ndcg3Shifted = usedCases == 0 ? 0.0 : ndcgShifted.Average();

            const double mapWeight = 0.7;
            const double ndcgWeight = 0.3;
            var selectionScore =
                mapWeight * (mapShifted - mapBaseline) +
                ndcgWeight * (ndcg3Shifted - ndcg3Baseline);

            return new SelectionMetrics(
                MapBaseline: mapBaseline,
                MapShifted: mapShifted,
                Ndcg3Baseline: ndcg3Baseline,
                Ndcg3Shifted: ndcg3Shifted,
                SelectionScore: selectionScore);
        }

        private static int FindRank<T>(IReadOnlyList<T> ranked, string relevantDocId)
        {
            for (var i = 0; i < ranked.Count; i++)
            {
                var candidate = ranked[i];
                var docIdProp = candidate?.GetType().GetProperty("DocId");
                var docId = docIdProp?.GetValue(candidate) as string;

                if (string.Equals(docId, relevantDocId, StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1;
                }
            }

            return -1;
        }

        private static double DcgatK(int rank, int k)
        {
            if (rank <= 0 || rank > k)
                return 0.0;

            return 1.0 / Math.Log(rank + 1, 2);
        }

        private sealed record SelectionMetrics(
            double MapBaseline,
            double MapShifted,
            double Ndcg3Baseline,
            double Ndcg3Shifted,
            double SelectionScore)
        {
            public double DeltaMap => MapShifted - MapBaseline;
            public double DeltaNdcg3 => Ndcg3Shifted - Ndcg3Baseline;
        }

        private static double CosineSimilarity(float[] a, float[] b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a.Length != b.Length)
                throw new InvalidOperationException("Embedding vectors must have the same dimension for cosine similarity.");

            double dot = 0.0;
            double normA = 0.0;
            double normB = 0.0;

            for (var i = 0; i < a.Length; i++)
            {
                var av = a[i];
                var bv = b[i];

                dot += av * bv;
                normA += av * av;
                normB += bv * bv;
            }

            if (normA <= 0.0 || normB <= 0.0)
            {
                return 0.0;
            }

            return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        private static string ResolveDomainRoot()
        {
            // Use the domain descriptor to locate the Mini-Insurance samples.
            // The actual layout (samples/insurance/...) is centralized in
            // MiniInsuranceDataset, which MiniInsuranceDomain delegates to.
            return MiniInsurance.MiniInsuranceDomain.GetSamplesRoot();
        }

        private static bool IsEnvEnabled(string name)
        {
            var v = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(v))
                return false;

            return v.Equals("1", StringComparison.OrdinalIgnoreCase)
                || v.Equals("true", StringComparison.OrdinalIgnoreCase)
                || v.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || v.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private sealed record QueryDefinition(string Id, string Text, string RelevantDocId);
    }
}
