using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.ConsoleEval.Repositories;
using EmbeddingShift.ConsoleEval.MiniInsurance;
using EmbeddingShift.Core.Infrastructure;
using EmbeddingShift.Core.Stats;
using EmbeddingShift.Workflows;

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

        public static async Task RunAsync(EmbeddingBackend backend, bool useLatest = false, double scale = 1.0)
        {
            var startedAtUtc = DateTimeOffset.UtcNow;

            var resultsRoot = MiniInsurancePaths.GetDomainRoot();

            var repository = new FileSystemShiftTrainingResultRepository(resultsRoot);

            var trainingResult = useLatest
                ? repository.LoadLatest(WorkflowName)
                : repository.LoadBest(WorkflowName);

            if (trainingResult is null)
            {
                if (!useLatest)
                {
                    // LoadBest() filters cancelled results by default. If everything is cancelled (|Δ|≈0), prefer baseline.
                    var cancelled = repository.LoadBest(WorkflowName, includeCancelled: true);
                    if (cancelled is not null && cancelled.IsCancelled)
                    {
                        Console.WriteLine("[MiniInsurancePosNegRunner] Training results exist but are cancelled (|Δ|≈0). Running baseline.");
                    }
                    else
                    {
                        Console.WriteLine(
                            $"[MiniInsurancePosNegRunner] No training result found for workflow '{WorkflowName}' under '{resultsRoot}'.");
                    }
                }
                else
                {
                    Console.WriteLine(
                        $"[MiniInsurancePosNegRunner] No training result found for workflow '{WorkflowName}' under '{resultsRoot}'.");
                }
}
            else
            {
                var len = trainingResult.DeltaVector?.Length ?? 0;
                var sel = useLatest ? "latest" : "best";
                Console.WriteLine(
                    $"[MiniInsurancePosNegRunner] Loaded {sel} training result: CreatedUtc={trainingResult.CreatedUtc:O}, " +
                    $"Cancelled={trainingResult.IsCancelled}, |Δ|={trainingResult.DeltaNorm:E3}, Δlen={len}, " +
                    $"dFirst+Δ={trainingResult.ImprovementFirstPlusDelta:0.000}");
            }

            var rawShift =
                (trainingResult is not null &&
                 trainingResult.IsCancelled == false &&
                 trainingResult.DeltaVector is { Length: > 0 })
                    ? trainingResult.DeltaVector
                    : null;

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

            float[] shift;
            if (rawShift is null || rawShift.Length == 0)
            {
                Console.WriteLine(
                    $"[MiniInsurancePosNegRunner] No usable (non-cancelled) delta vector found for workflow '{WorkflowName}'. " +
                    "Running with a zero shift (baseline).");

                shift = new float[dim];
            }
            else
            {
                if (rawShift.Length != dim)
                {
                    throw new InvalidOperationException(
                        $"Shift dimension ({rawShift.Length}) does not match embedding dimension ({dim}).");
                }

                shift = rawShift;
            }
            if (Math.Abs(scale - 1.0) > 1e-12)
            {
                var s = (float)scale;
                var scaled = new float[shift.Length];
                for (var i = 0; i < shift.Length; i++)
                    scaled[i] = shift[i] * s;

                shift = scaled;
                Console.WriteLine($"[MiniInsurancePosNegRunner] Applying shift scale: {scale:0.###}");
            }

            var apBaseline = new List<double>();
            var ndcgBaseline = new List<double>();
            var apShifted = new List<double>();
            var ndcgShifted = new List<double>();

            var perQueryBaseline = new List<PerQueryEval>(queries.Count);
            var perQueryPosNeg = new List<PerQueryEval>(queries.Count);

            foreach (var q in queries)
            {
                if (string.IsNullOrWhiteSpace(q.RelevantDocId) ||
                    !docEmbeddings.ContainsKey(q.RelevantDocId))
                {
                    continue;
                }

                var qText = q.Text ?? string.Empty;
                var qEmbMaybe = await provider.GetEmbeddingAsync(qText).ConfigureAwait(false);
                if (qEmbMaybe is null || qEmbMaybe.Length != dim)
                    continue;

                var qEmb = qEmbMaybe!; // non-null for closures / nullable flow
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

                var topB = rankedBaseline.Count > 0 ? rankedBaseline[0] : null;
                var topB2 = rankedBaseline.Count > 1 ? rankedBaseline[1] : null;
                var topB3 = rankedBaseline.Count > 2 ? rankedBaseline[2] : null;

                var topS = rankedShifted.Count > 0 ? rankedShifted[0] : null;
                var topS2 = rankedShifted.Count > 1 ? rankedShifted[1] : null;
                var topS3 = rankedShifted.Count > 2 ? rankedShifted[2] : null;

                // when writing PerQueryEval for baseline
                perQueryBaseline.Add(new PerQueryEval(
                    QueryId: q.Id,
                    RelevantDocId: q.RelevantDocId,
                    Rank: rankBaseline,
                    Ap1: rankBaseline == 0 ? 0.0 : (1.0 / rankBaseline),
                    Ndcg3: ndcgB,
                    TopDocId: topB?.DocId,
                    TopScore: topB?.Score ?? 0.0,
                    Top2DocId: topB2?.DocId,
                    Top2Score: topB2?.Score ?? 0.0,
                    Top3DocId: topB3?.DocId,
                    Top3Score: topB3?.Score ?? 0.0));

                // when writing PerQueryEval for posneg
                perQueryPosNeg.Add(new PerQueryEval(
                    QueryId: q.Id,
                    RelevantDocId: q.RelevantDocId,
                    Rank: rankShifted,
                    Ap1: rankShifted == 0 ? 0.0 : (1.0 / rankShifted),
                    Ndcg3: ndcgS,
                    TopDocId: topS?.DocId,
                    TopScore: topS?.Score ?? 0.0,
                    Top2DocId: topS2?.DocId,
                    Top2Score: topS2?.Score ?? 0.0,
                    Top3DocId: topS3?.DocId,
                    Top3Score: topS3?.Score ?? 0.0));
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
            // Keep user-facing artifacts under the standard runs folder; internal run metadata lives under runs\_repo.
            //   <tenant>\runs\mini-insurance-posneg-run_YYYYMMDD_HHMMSS_fff
            var runsRoot = MiniInsurancePaths.GetRunsRoot();
            Directory.CreateDirectory(runsRoot);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var runDirName = $"mini-insurance-posneg-run_{timestamp}";
            var runDir = Path.Combine(runsRoot, runDirName);
            Directory.CreateDirectory(runDir);

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

            await File.WriteAllTextAsync(
                    Path.Combine(runDir, "eval.perQuery.baseline.json"),
                    JsonSerializer.Serialize(perQueryBaseline, jsonOptions))
                .ConfigureAwait(false);

            await File.WriteAllTextAsync(
                    Path.Combine(runDir, "eval.perQuery.posneg.json"),
                    JsonSerializer.Serialize(perQueryPosNeg, jsonOptions))
                .ConfigureAwait(false);

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

            // Demo boost: write top-N per-query examples (rank changes + top hits) for quick inspection.
            // This is intentionally a human-readable artifact under the same run directory.
            TryWriteExamplesMarkdown(runDir, perQueryBaseline, perQueryPosNeg, queries, docs);

            var finishedAtUtc = DateTimeOffset.UtcNow;

            // Additionally persist run.json artifacts under results/.../runs so generic tooling
            // like runs-compare can pick these up without parsing custom metric files.
            // This intentionally produces two comparable runs:
            //  - MiniInsurance-PosNeg-Baseline
            //  - MiniInsurance-PosNeg
            var runRepo = new FileRunRepository(runsRoot);

            var baselineArtifact = new WorkflowRunArtifact(
                RunId: Guid.NewGuid().ToString("N"),
                WorkflowName: "MiniInsurance-PosNeg-Baseline",
                StartedUtc: startedAtUtc,
                FinishedUtc: finishedAtUtc,
                Success: true,
                Metrics: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["map@1"] = mapBaseline,
                    ["ndcg@3"] = ndcg3Baseline,
                },
                Notes: "Baseline metrics captured from mini-insurance posneg-run (single relevant doc per query)."
            );

            var posnegArtifact = new WorkflowRunArtifact(
                RunId: Guid.NewGuid().ToString("N"),
                WorkflowName: "MiniInsurance-PosNeg",
                StartedUtc: startedAtUtc,
                FinishedUtc: finishedAtUtc,
                Success: true,
                Metrics: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    ["map@1"] = mapShifted,
                    ["ndcg@3"] = ndcg3Shifted,
                },
                Notes: "PosNeg metrics captured from mini-insurance posneg-run (query shift = query + learned delta)."
            );

            await runRepo.SaveAsync(baselineArtifact).ConfigureAwait(false);
            await runRepo.SaveAsync(posnegArtifact).ConfigureAwait(false);

        }

        
                private static void TryWriteExamplesMarkdown(
            string runDir,
            IReadOnlyList<PerQueryEval> baseline,
            IReadOnlyList<PerQueryEval> posneg,
            IReadOnlyList<QueryDefinition> queries,
            IReadOnlyDictionary<string, string> docs)
        {
            try
            {
                const int defaultTopN = 10;
                const int topK = 3;

                var topN = defaultTopN;
                var raw = Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_EXAMPLES_TOPN");
                if (!string.IsNullOrWhiteSpace(raw) &&
                    int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
                    parsed > 0)
                {
                    topN = parsed;
                }

                static int NormRank(int rank) => rank <= 0 ? 1_000_000 : rank;
                static bool InTopK(int rank, int k) => rank > 0 && rank <= k;

                var qText = queries
                    .Where(q => !string.IsNullOrWhiteSpace(q.Id))
                    .GroupBy(q => q.Id)
                    .ToDictionary(g => g.Key, g => g.First().Text ?? string.Empty, StringComparer.OrdinalIgnoreCase);

                var b = baseline.ToDictionary(x => x.QueryId, StringComparer.OrdinalIgnoreCase);
                var p = posneg.ToDictionary(x => x.QueryId, StringComparer.OrdinalIgnoreCase);

                var rows = new List<(string QueryId, int RankB, int RankP, int Delta, PerQueryEval B, PerQueryEval P)>();

                foreach (var kv in b)
                {
                    if (!p.TryGetValue(kv.Key, out var pp))
                        continue;

                    var bb = kv.Value;

                    // delta > 0 => improvement (rank got smaller)
                    // Note: rank can be <=0 (not found). Normalize so "not found -> found" is treated as improvement.
                    var delta = NormRank(bb.Rank) - NormRank(pp.Rank);

                    rows.Add((bb.QueryId, bb.Rank, pp.Rank, delta, bb, pp));
                }

                var improvedCount = rows.Count(r => r.Delta > 0);
                var regressedCount = rows.Count(r => r.Delta < 0);

                var promotedToTop1Count = rows.Count(r => r.RankP == 1 && r.RankB != 1);
                var promotedToTop3Count = rows.Count(r => !InTopK(r.RankB, topK) && InTopK(r.RankP, topK));
                var demotedFromTop3Count = rows.Count(r => InTopK(r.RankB, topK) && !InTopK(r.RankP, topK));

                // Selection strategy for demo impact:
                // 1) Threshold crossings (Top-1 / Top-3)
                // 2) Largest rank gains
                // 3) Demotions out of Top-3 (honest downside)
                // 4) Worst regressions
                var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                List<(string QueryId, int RankB, int RankP, int Delta, PerQueryEval B, PerQueryEval P)> TakeUnique(
                    IEnumerable<(string QueryId, int RankB, int RankP, int Delta, PerQueryEval B, PerQueryEval P)> src,
                    int n)
                {
                    var list = new List<(string, int, int, int, PerQueryEval, PerQueryEval)>(n);
                    foreach (var r in src)
                    {
                        if (list.Count >= n) break;
                        if (selected.Add(r.QueryId))
                            list.Add(r);
                    }
                    return list;
                }

                var promotedToTop1 = TakeUnique(
                    rows.Where(r => r.RankP == 1 && r.RankB != 1)
                        .OrderByDescending(r => NormRank(r.RankB))
                        .ThenBy(r => r.QueryId),
                    topN);

                var promotedToTop3 = TakeUnique(
                    rows.Where(r => !InTopK(r.RankB, topK) && InTopK(r.RankP, topK))
                        .OrderByDescending(r => r.Delta)
                        .ThenBy(r => r.RankP)
                        .ThenBy(r => r.QueryId),
                    topN);

                var largestGains = TakeUnique(
                    rows.Where(r => r.Delta > 0)
                        .OrderByDescending(r => r.Delta)
                        .ThenBy(r => r.RankP)
                        .ThenBy(r => r.QueryId),
                    topN);

                var demotedFromTop3 = TakeUnique(
                    rows.Where(r => InTopK(r.RankB, topK) && !InTopK(r.RankP, topK))
                        .OrderBy(r => r.Delta) // most negative first
                        .ThenBy(r => r.RankP)
                        .ThenBy(r => r.QueryId),
                    topN);

                var worstRegressions = TakeUnique(
                    rows.Where(r => r.Delta < 0)
                        .OrderBy(r => r.Delta) // most negative first
                        .ThenBy(r => r.RankP)
                        .ThenBy(r => r.QueryId),
                    topN);

                var sb = new StringBuilder();
                sb.AppendLine("# Mini Insurance PosNeg Examples");
                sb.AppendLine();
                sb.AppendLine($"Generated (UTC): {DateTime.UtcNow:O}");
                sb.AppendLine($"TopN: {topN}");
                sb.AppendLine($"TopK: {topK}");
                sb.AppendLine();
                sb.AppendLine($"Total comparable queries: {rows.Count}");
                sb.AppendLine($"Improved (dRank>0): {improvedCount}");
                sb.AppendLine($"Regressed (dRank<0): {regressedCount}");
                sb.AppendLine($"Promoted to #1: {promotedToTop1Count}");
                sb.AppendLine($"Promoted into Top-{topK}: {promotedToTop3Count}");
                sb.AppendLine($"Demoted out of Top-{topK}: {demotedFromTop3Count}");
                sb.AppendLine();

                void AppendSection(string title, IReadOnlyList<(string QueryId, int RankB, int RankP, int Delta, PerQueryEval B, PerQueryEval P)> section)
                {
                    if (section.Count <= 0) return;
                    sb.AppendLine($"## {title}");
                    sb.AppendLine();
                    AppendExamples(sb, section, qText, docs, topK);
                    sb.AppendLine();
                }

                AppendSection("Promoted into #1", promotedToTop1);
                AppendSection($"Promoted into Top-{topK}", promotedToTop3);
                AppendSection("Largest rank gains", largestGains);
                AppendSection($"Demoted out of Top-{topK}", demotedFromTop3);
                AppendSection("Worst regressions", worstRegressions);

                var path = Path.Combine(runDir, "examples-posneg.md");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch
            {
                // best-effort only
            }
        }

        private static void AppendExamples(
            StringBuilder sb,
            IReadOnlyList<(string QueryId, int RankB, int RankP, int Delta, PerQueryEval B, PerQueryEval P)> rows,
            IReadOnlyDictionary<string, string> queryText,
            IReadOnlyDictionary<string, string> docs,
            int topK)
        {
            static string Trunc(string? s, int max)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                s = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
                return s.Length <= max ? s : s.Substring(0, max) + "...";
            }

            static string RankText(int rank)
                => rank <= 0 ? "<not-found>" : rank.ToString(CultureInfo.InvariantCulture);

            static bool InTopK(int rank, int k) => rank > 0 && rank <= k;

            static string FormatHit(string? docId, double score, string relevantDocId)
            {
                if (string.IsNullOrWhiteSpace(docId))
                    return string.Empty;

                var s = $"{docId} ({score:0.###})";
                return string.Equals(docId, relevantDocId, StringComparison.OrdinalIgnoreCase)
                    ? $"**{s}**"
                    : s;
            }

            static string HitLine(PerQueryEval e, string relevantDocId)
            {
                var hits = new List<string>(3);

                var h1 = FormatHit(e.TopDocId, e.TopScore, relevantDocId);
                if (!string.IsNullOrWhiteSpace(h1)) hits.Add(h1);

                var h2 = FormatHit(e.Top2DocId, e.Top2Score, relevantDocId);
                if (!string.IsNullOrWhiteSpace(h2)) hits.Add(h2);

                var h3 = FormatHit(e.Top3DocId, e.Top3Score, relevantDocId);
                if (!string.IsNullOrWhiteSpace(h3)) hits.Add(h3);

                return hits.Count == 0 ? "<none>" : string.Join(", ", hits);
            }

            static (int Pos, double Score) FindRelevantInTop3(PerQueryEval e, string relevantDocId)
            {
                if (!string.IsNullOrWhiteSpace(e.TopDocId) &&
                    string.Equals(e.TopDocId, relevantDocId, StringComparison.OrdinalIgnoreCase))
                    return (1, e.TopScore);

                if (!string.IsNullOrWhiteSpace(e.Top2DocId) &&
                    string.Equals(e.Top2DocId, relevantDocId, StringComparison.OrdinalIgnoreCase))
                    return (2, e.Top2Score);

                if (!string.IsNullOrWhiteSpace(e.Top3DocId) &&
                    string.Equals(e.Top3DocId, relevantDocId, StringComparison.OrdinalIgnoreCase))
                    return (3, e.Top3Score);

                return (0, 0.0);
            }

            static string DocHint(IReadOnlyDictionary<string, string> docs, string? docId)
            {
                if (string.IsNullOrWhiteSpace(docId)) return string.Empty;
                if (!docs.TryGetValue(docId, out var text)) return string.Empty;

                // Use the first non-empty line as a lightweight hint.
                var line = text
                    .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                return string.IsNullOrWhiteSpace(line) ? string.Empty : Trunc(line, 80);
            }

            static string DRankText(int rankB, int rankP, int delta)
            {
                if (rankB <= 0 && rankP <= 0) return "0";
                if (rankB <= 0) return $"+NF→{rankP}";
                if (rankP <= 0) return $"-{rankB}→NF";
                return delta.ToString("+0;-0;0", CultureInfo.InvariantCulture);
            }

            var i = 1;
            foreach (var r in rows)
            {
                var rel = r.B.RelevantDocId;

                sb.AppendLine($"### {i}) {r.QueryId} (dRank {DRankText(r.RankB, r.RankP, r.Delta)})");
                sb.AppendLine();

                if (queryText.TryGetValue(r.QueryId, out var qt) && !string.IsNullOrWhiteSpace(qt))
                    sb.AppendLine($"Query: {Trunc(qt, 160)}");

                sb.AppendLine($"RelevantDocId: {rel}");

                var relHint = DocHint(docs, rel);
                if (!string.IsNullOrWhiteSpace(relHint))
                    sb.AppendLine($"Relevant hint: {relHint}");

                var inTopB = FindRelevantInTop3(r.B, rel);
                var inTopP = FindRelevantInTop3(r.P, rel);

                sb.AppendLine();
                sb.AppendLine($"Baseline rank: {RankText(r.RankB)}");
                sb.AppendLine($"PosNeg   rank: {RankText(r.RankP)}");
                sb.AppendLine($"Relevant in Top-{topK}: baseline={(InTopK(r.RankB, topK) ? "YES" : "NO")}, posneg={(InTopK(r.RankP, topK) ? "YES" : "NO")}");
                if (inTopB.Pos > 0 || inTopP.Pos > 0)
                {
                    var bInfo = inTopB.Pos > 0 ? $"#{inTopB.Pos} ({inTopB.Score:0.###})" : "NO";
                    var pInfo = inTopP.Pos > 0 ? $"#{inTopP.Pos} ({inTopP.Score:0.###})" : "NO";
                    sb.AppendLine($"Relevant in Top3 details: baseline={bInfo}, posneg={pInfo}");
                }

                var bTop1 = r.B.TopDocId ?? "<none>";
                var pTop1 = r.P.TopDocId ?? "<none>";
                sb.AppendLine($"Top1: baseline={bTop1}, posneg={pTop1}{(string.Equals(bTop1, pTop1, StringComparison.OrdinalIgnoreCase) ? "" : " (changed)")}");

                sb.AppendLine();
                sb.AppendLine($"Baseline top3: {HitLine(r.B, rel)}");
                sb.AppendLine($"PosNeg   top3: {HitLine(r.P, rel)}");
                sb.AppendLine();
                i++;
            }
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
