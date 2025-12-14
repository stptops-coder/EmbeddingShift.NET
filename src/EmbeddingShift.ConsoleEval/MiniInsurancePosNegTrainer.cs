using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.ConsoleEval.Repositories;
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

        public static async Task<ShiftTrainingResult> TrainAsync(EmbeddingBackend backend)
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

            // TODO (Production-scale / real domains):
            // 1) Hard negatives: do NOT score against all docs. Use candidate set from ANN / Top-K retrieval (e.g., K=50..200).
            //    Learn only from "hard" negatives (high score, wrong top-1) to keep training efficient and signal strong.
            // 2) Safety / gating: train on one split, validate on held-out split; promote only if improves and does not regress
            //    key queries. Keep rollback path and store "best" result per scope/workflow.
            // 3) Weighting modes (optional later): uniform vs margin vs damped-margin (log1p/sqrt) for robustness across
            //    deterministic/stochastic/real backends.
            // 4) Metadata + reproducibility: persist weighting mode, K, clip settings, provider(sim/stochastic/real), run-id,
            //    timestamps, and metrics next to the learned delta.
            // 5) Diagnostics: if |Δ| is tiny, detect potential cancel-out cycles and log a hint (debug mode already exists).
            var sumDirection = new float[dim];
            var trainingCases = 0;

            // Diagnostics via env flags (off by default).
            var debug = IsEnvEnabled("EMBEDDINGSHIFT_POSNEG_DEBUG");
            var disableNormClip = IsEnvEnabled("EMBEDDINGSHIFT_POSNEG_NOCLIP");

            double sumCaseNorm = 0.0;
            double minCaseNorm = double.MaxValue;
            double maxCaseNorm = 0.0;
            var zeroCaseCount = 0;

            var uniquePairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var q in queries)
            {
                if (string.IsNullOrWhiteSpace(q.RelevantDocId) ||
                    !docEmbeddings.ContainsKey(q.RelevantDocId))
                {
                    continue;
                }

                var qText = q.Text ?? string.Empty;
                var qEmb = await provider.GetEmbeddingAsync(qText).ConfigureAwait(false);

                if (qEmb == null)
                    continue;

                if (qEmb.Length != dim)
                    throw new InvalidOperationException("Query embedding dimension does not match document embeddings.");

                var ranked = docEmbeddings
                    .Select(d => new
                    {
                        DocId = d.Key,
                        Score = CosineSimilarity(qEmb, d.Value)
                    })
                    .OrderByDescending(x => x.Score)
                    .ToList();

                var posIndex = ranked.FindIndex(r =>
                    string.Equals(r.DocId, q.RelevantDocId, StringComparison.OrdinalIgnoreCase));

                // We only learn from actual mistakes: positive document exists and is not already ranked first.
                if (posIndex <= 0)
                {
                    continue;
                }

                var negCandidate = ranked[0];
                if (string.Equals(negCandidate.DocId, q.RelevantDocId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var pos = docEmbeddings[q.RelevantDocId];
                var neg = docEmbeddings[negCandidate.DocId];

                if (pos.Length != dim || neg.Length != dim)
                    throw new InvalidOperationException("Positive/negative embeddings have inconsistent dimensions.");

                uniquePairs.Add($"{q.Id}|{q.RelevantDocId}|{negCandidate.DocId}");

                var caseNormSquared = 0.0;
                for (var i = 0; i < dim; i++)
                {
                    var d = pos[i] - neg[i];
                    sumDirection[i] += d;
                    caseNormSquared += d * d;
                }

                var caseNorm = Math.Sqrt(caseNormSquared);
                sumCaseNorm += caseNorm;
                if (caseNorm < 1e-9) zeroCaseCount++;
                if (caseNorm < minCaseNorm) minCaseNorm = caseNorm;
                if (caseNorm > maxCaseNorm) maxCaseNorm = caseNorm;

                if (debug)
                {
                    Console.WriteLine(
                        $"[PosNeg] Case {trainingCases + 1}: q={q.Id}, pos={q.RelevantDocId}, neg={negCandidate.DocId}, posRank={posIndex}, |dir|={caseNorm:0.000000}");
                }

                trainingCases++;

            }

            if (trainingCases == 0)
                throw new InvalidOperationException("No suitable training cases were found for pos-neg training.");

            var shift = new float[dim];
            for (var i = 0; i < dim; i++)
            {
                shift[i] = sumDirection[i] / trainingCases;
            }

            // Optional L2 norm clipping to keep the learned shift in a safe range.
            // Can be disabled via: EMBEDDINGSHIFT_POSNEG_NOCLIP=1
            const double MaxL2Norm = 1.5;
            var normSquared = 0.0;
            for (var i = 0; i < dim; i++)
            {
                var v = shift[i];
                normSquared += v * v;
            }

            var norm = Math.Sqrt(normSquared);
            var clipped = false;

            if (!disableNormClip && norm > MaxL2Norm && norm > 0.0)
            {
                var scale = MaxL2Norm / norm;
                for (var i = 0; i < dim; i++)
                {
                    shift[i] = (float)(shift[i] * scale);
                }

                clipped = true;
            }

            var postNorm = clipped ? MaxL2Norm : norm;

            if (debug || postNorm < 1e-6)
            {
                var avgCaseNorm = trainingCases > 0 ? (sumCaseNorm / trainingCases) : 0.0;
                if (minCaseNorm == double.MaxValue) minCaseNorm = 0.0;

                Console.WriteLine(
                    $"[PosNeg] Summary: cases={trainingCases}, uniquePairs={uniquePairs.Count}, avg|dir|={avgCaseNorm:0.000000}, min|dir|={minCaseNorm:0.000000}, max|dir|={maxCaseNorm:0.000000}, zeroDirs={zeroCaseCount}");

                var clipState = disableNormClip ? "disabled" : (clipped ? "applied" : "none");
                Console.WriteLine(
                    $"[PosNeg] Learned delta: |Δ|={postNorm:0.000000} (preClip={norm:0.000000}, clip={clipState}, max={MaxL2Norm:0.0})");

                if (!debug && postNorm < 1e-6)
                    Console.WriteLine("[PosNeg] Hint: set EMBEDDINGSHIFT_POSNEG_DEBUG=1 to print per-case details.");
            }

            var resultsRoot = DirectoryLayout.ResolveResultsRoot("insurance");

            var trainingResult = new ShiftTrainingResult
            {
                WorkflowName = WorkflowName,
                CreatedUtc = DateTime.UtcNow,
                BaseDirectory = resultsRoot,
                ComparisonRuns = trainingCases,
                ImprovementFirst = 0.0,
                ImprovementFirstPlusDelta = 0.0,
                DeltaImprovement = 0.0,
                DeltaVector = shift,
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
