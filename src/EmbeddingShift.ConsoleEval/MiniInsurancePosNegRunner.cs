using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.ConsoleEval.Repositories;
using EmbeddingShift.Core.Infrastructure;

namespace EmbeddingShift.ConsoleEval
{
    /// <summary>
    /// Runs a simple retrieval experiment using the learned pos-neg Delta vector:
    /// - Baseline: no shift
    /// - Shifted:  query + learned pos-neg vector
    /// Metrics: MAP@1 and NDCG@3 over the mini-insurance sample.
    /// </summary>
    public static class MiniInsurancePosNegRunner
    {
        private const string WorkflowName = "mini-insurance-posneg";

        public static async Task RunAsync(EmbeddingBackend backend)
        {
            var resultsRoot = DirectoryLayout.ResolveResultsRoot("insurance");
            var repository = new FileSystemShiftTrainingResultRepository(resultsRoot);

            var trainingResult = repository.LoadBest(WorkflowName);

            // We may not have training artifacts yet (fresh repo / clean results folder).
            // In that case we run in "baseline mode" using a zero shift vector.
            // We can only size the zero vector once we know the embedding dimension.
            var rawShift = trainingResult?.IsCancelled == true
                ? null
                : trainingResult?.DeltaVector;
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

            var json = await File.ReadAllTextAsync(queriesPath).ConfigureAwait(false);
            var queries = JsonSerializer.Deserialize<List<QueryDefinition>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<QueryDefinition>();

            if (queries.Count == 0)
                throw new InvalidOperationException("No queries found in queries.json.");

            // Embed all documents once using the same provider as the trainer.
            var docEmbeddings = new Dictionary<string, float[]>(docs.Count, StringComparer.OrdinalIgnoreCase);
            float[]? firstEmbedding = null;

            foreach (var kvp in docs)
            {
                var emb = await provider.GetEmbeddingAsync(kvp.Value).ConfigureAwait(false);
                if (emb == null)
                    throw new InvalidOperationException("Embedding provider returned a null embedding for a document.");

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
                throw new InvalidOperationException("No document embeddings were created.");

            var dim = firstEmbedding.Length;

            var shift = rawShift;
            if (shift is null || shift.Length == 0)
            {
                Console.WriteLine(
                    $"[MiniInsurancePosNegRunner] No usable (non-cancelled) delta vector found for workflow '{WorkflowName}'. " +
                    "Running with a zero shift (baseline).");

                shift = new float[dim];
            }
            else if (shift.Length != dim)
            {
                throw new InvalidOperationException(
                    $"Shift dimension ({shift.Length}) does not match embedding dimension ({dim}).");
            }

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

                var qText = q.Text ?? string.Empty;
                var qEmb = await provider.GetEmbeddingAsync(qText).ConfigureAwait(false);
                if (qEmb == null || qEmb.Length != dim)
                    continue;

                // Baseline ranking (no shift).
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
                {
                    continue;
                }

                var qShifted = new float[dim];
                for (var i = 0; i < dim; i++)
                {
                    qShifted[i] = qEmb[i] + shift[i];
                }

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
                {
                    continue;
                }

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

            Console.WriteLine($"[MiniInsurance] PosNeg run over {docs.Count} policies and {usedCases} effective queries.");
            Console.WriteLine();
            Console.WriteLine($"  Baseline   MAP@1 = {mapBaseline:0.000},  NDCG@3 = {ndcg3Baseline:0.000}");
            Console.WriteLine($"  PosNeg     MAP@1 = {mapShifted:0.000},  NDCG@3 = {ndcg3Shifted:0.000}");
            Console.WriteLine();
            Console.WriteLine($"  Delta      MAP@1 = {mapShifted - mapBaseline:+0.000;-0.000;0.000}");
            Console.WriteLine($"             NDCG@3 = {ndcg3Shifted - ndcg3Baseline:+0.000;-0.000;0.000}");

            // Persist metrics for later inspection/aggregation.
            // We create a small run directory under resultsRoot:
            //   mini-insurance-posneg-run_YYYYMMDD_HHMMSS_fff
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var runDirName = $"mini-insurance-posneg-run_{timestamp}";
            var runDir = Path.Combine(resultsRoot, runDirName);
            Directory.CreateDirectory(runDir);

            var metrics = new
            {
                WorkflowName,
                CreatedUtc = DateTime.UtcNow,
                PolicyCount = docs.Count,
                EffectiveQueryCount = usedCases,
                MapBaseline = mapBaseline,
                MapPosNeg = mapShifted,
                DeltaMap = mapShifted - mapBaseline,
                Ndcg3Baseline = ndcg3Baseline,
                Ndcg3PosNeg = ndcg3Shifted,
                DeltaNdcg3 = ndcg3Shifted - ndcg3Baseline
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var jsonPath = Path.Combine(runDir, "metrics-posneg.json");
            await File.WriteAllTextAsync(
                    jsonPath,
                    JsonSerializer.Serialize(metrics, jsonOptions))
                .ConfigureAwait(false);

            var mdBuilder = new System.Text.StringBuilder();
            mdBuilder.AppendLine("# Mini Insurance PosNeg Metrics");
            mdBuilder.AppendLine();
            mdBuilder.AppendLine($"Created (UTC): {metrics.CreatedUtc:O}");
            mdBuilder.AppendLine();
            mdBuilder.AppendLine($"Workflow   : {metrics.WorkflowName}");
            mdBuilder.AppendLine($"Policies   : {metrics.PolicyCount}");
            mdBuilder.AppendLine($"Queries    : {metrics.EffectiveQueryCount}");
            mdBuilder.AppendLine();
            mdBuilder.AppendLine("## Metrics");
            mdBuilder.AppendLine();
            mdBuilder.AppendLine("| Metric | Baseline | PosNeg | Delta |");
            mdBuilder.AppendLine("|--------|----------|--------|-------|");
            mdBuilder.AppendLine($"| MAP@1  | {metrics.MapBaseline:0.000} | {metrics.MapPosNeg:0.000} | {metrics.DeltaMap:+0.000;-0.000;0.000} |");
            mdBuilder.AppendLine($"| NDCG@3 | {metrics.Ndcg3Baseline:0.000} | {metrics.Ndcg3PosNeg:0.000} | {metrics.DeltaNdcg3:+0.000;-0.000;0.000} |");

            var mdPath = Path.Combine(runDir, "metrics-posneg.md");
            await File.WriteAllTextAsync(mdPath, mdBuilder.ToString())
                .ConfigureAwait(false);
        }

        private static int FindRank(
            IReadOnlyList<dynamic> ranked,
            string relevantDocId)
        {
            for (var i = 0; i < ranked.Count; i++)
            {
                if (string.Equals(ranked[i].DocId, relevantDocId, StringComparison.OrdinalIgnoreCase))
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

            return 1.0 / Math.Log(rank + 1, 2.0);
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
            // Centralized layout: repo-root/samples/insurance
            // (shared with FileBasedInsuranceMiniWorkflow and MiniInsurancePosNegTrainer).
            return EmbeddingShift.Workflows.Domains.MiniInsuranceDataset.ResolveDatasetRoot();
        }

        private sealed record QueryDefinition(string Id, string Text, string RelevantDocId);
    }
}
