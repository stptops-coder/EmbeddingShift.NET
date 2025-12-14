using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.ConsoleEval.Repositories;
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
            float cancelOutEpsilon)

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

            var sumDirection = new float[dim];

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
                Debug: debug);

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

            var resultsRoot = DirectoryLayout.ResolveResultsRoot("insurance");

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
                ScopeId = MiniInsuranceScopes.DefaultScopeId
            };

            var repository = new FileSystemShiftTrainingResultRepository(resultsRoot);
            repository.Save(trainingResult);

            return trainingResult;
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
