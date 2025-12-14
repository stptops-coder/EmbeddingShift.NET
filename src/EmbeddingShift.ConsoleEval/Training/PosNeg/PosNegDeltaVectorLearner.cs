using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Utils;

namespace EmbeddingShift.Core.Training.PosNeg
{
    public sealed record PosNegTrainingQuery(string QueryId, string Text, string RelevantDocId);

    public sealed record PosNegLearningOptions(
        float MaxL2Norm,
        bool DisableNormClip,
        bool Debug);

    public sealed record PosNegLearningStats(
        int Cases,
        int UniquePairs,
        double AvgDirectionNorm,
        double MinDirectionNorm,
        double MaxDirectionNorm,
        int ZeroDirections,
        bool NormClipApplied,
        double PreClipDeltaNorm,
        double PostClipDeltaNorm,
        bool CancelOutSuspected);

    public sealed record PosNegLearningResult(
        float[] DeltaVector,
        PosNegLearningStats Stats);

    public static class PosNegDeltaVectorLearner
    {
        // TODO (future):
        // - Hard-negative sampling / TopK candidate sets (ANN index) for scale.
        // - Optional weighting modes (e.g., margin-weighted) to reduce cancellation when uniform averaging cancels out.
        // - Safety gates (validate/promote/rollback) and richer metadata persistence (mode/topK/clip/provider).
        public static async Task<PosNegLearningResult> LearnAsync(
            IEmbeddingProvider provider,
            IReadOnlyList<PosNegTrainingQuery> queries,
            IReadOnlyDictionary<string, float[]> docEmbeddings,
            PosNegLearningOptions options,
            Action<string>? debugLog = null)
        {
            if (provider is null) throw new ArgumentNullException(nameof(provider));
            if (queries is null) throw new ArgumentNullException(nameof(queries));
            if (docEmbeddings is null) throw new ArgumentNullException(nameof(docEmbeddings));
            if (docEmbeddings.Count == 0) throw new ArgumentException("docEmbeddings must not be empty.", nameof(docEmbeddings));

            var first = docEmbeddings.First().Value ?? throw new ArgumentException("docEmbeddings contains a null embedding.", nameof(docEmbeddings));
            var dim = first.Length;
            if (dim <= 0) throw new ArgumentException("Embedding dimension must be > 0.", nameof(docEmbeddings));

            foreach (var (docId, emb) in docEmbeddings)
            {
                if (emb is null) throw new ArgumentException($"docEmbeddings contains a null embedding for '{docId}'.", nameof(docEmbeddings));
                if (emb.Length != dim) throw new ArgumentException($"Inconsistent embedding dim for '{docId}'. Expected {dim}, got {emb.Length}.", nameof(docEmbeddings));
            }

            var sumDirection = new float[dim];

            var trainingCases = 0;
            var uniquePairs = new HashSet<string>(StringComparer.Ordinal);

            double sumDirNorm = 0;
            double minDirNorm = double.PositiveInfinity;
            double maxDirNorm = 0;
            var zeroDirs = 0;

            for (var i = 0; i < queries.Count; i++)
            {
                var q = queries[i];
                if (!docEmbeddings.TryGetValue(q.RelevantDocId, out var posEmb))
                    throw new InvalidOperationException($"RelevantDocId '{q.RelevantDocId}' not found in docEmbeddings (query '{q.QueryId}').");

                var qEmb = await provider.GetEmbeddingAsync(q.Text).ConfigureAwait(false);

                var scored = new List<(string DocId, double Score)>(docEmbeddings.Count);
                foreach (var (docId, docEmb) in docEmbeddings)
                {
                    var sim = VectorOps.Cosine(qEmb, docEmb);
                    scored.Add((docId, sim));
                }

                scored.Sort(static (a, b) => b.Score.CompareTo(a.Score));

                var posIndex = scored.FindIndex(x => string.Equals(x.DocId, q.RelevantDocId, StringComparison.Ordinal));
                if (posIndex < 0)
                    throw new InvalidOperationException($"RelevantDocId '{q.RelevantDocId}' was not found in the ranking (query '{q.QueryId}').");

                // Only learn from "error-ish" cases: relevant doc is not top-1.
                if (posIndex == 0)
                    continue;

                var negDocId = scored[0].DocId;
                if (!docEmbeddings.TryGetValue(negDocId, out var negEmb))
                    throw new InvalidOperationException($"NegDocId '{negDocId}' not found in docEmbeddings (query '{q.QueryId}').");

                var pairKey = $"{q.RelevantDocId}|{negDocId}";
                uniquePairs.Add(pairKey);

                double dirNormSq = 0;
                for (var d = 0; d < dim; d++)
                {
                    var dir = posEmb[d] - negEmb[d];
                    sumDirection[d] += dir;
                    dirNormSq += (double)dir * dir;
                }

                if (dirNormSq <= 1e-18)
                {
                    zeroDirs++;
                }

                var dirNorm = Math.Sqrt(Math.Max(0, dirNormSq));
                sumDirNorm += dirNorm;
                minDirNorm = Math.Min(minDirNorm, dirNorm);
                maxDirNorm = Math.Max(maxDirNorm, dirNorm);

                trainingCases++;

                if (options.Debug && debugLog is not null)
                {
                    var posRank = posIndex + 1;
                    debugLog($"[PosNeg] Case {trainingCases}: q={q.QueryId}, pos={q.RelevantDocId}, neg={negDocId}, posRank={posRank}, |dir|={dirNorm:0.000000}");
                }
            }

            var delta = new float[dim];
            if (trainingCases > 0)
            {
                var inv = 1.0f / trainingCases;
                for (var d = 0; d < dim; d++)
                    delta[d] = sumDirection[d] * inv;
            }

            var preClip = VectorOps.Norm2(delta);
            var normClipApplied = false;

            if (!options.DisableNormClip && options.MaxL2Norm > 0 && preClip > options.MaxL2Norm)
            {
                var scale = options.MaxL2Norm / preClip;

                for (var d = 0; d < delta.Length; d++)
                    delta[d] *= scale;

                normClipApplied = true;
            }

            var postClip = VectorOps.Norm2(delta);

            var avgDirNorm = trainingCases > 0 ? (sumDirNorm / trainingCases) : 0.0;
            if (double.IsPositiveInfinity(minDirNorm)) minDirNorm = 0.0;

            // Cancel-out heuristic: strong per-case directions but near-zero averaged delta.
            var cancelOut = trainingCases > 0 && avgDirNorm > 1.0 && postClip < 1e-6;

            var stats = new PosNegLearningStats(
                Cases: trainingCases,
                UniquePairs: uniquePairs.Count,
                AvgDirectionNorm: avgDirNorm,
                MinDirectionNorm: minDirNorm,
                MaxDirectionNorm: maxDirNorm,
                ZeroDirections: zeroDirs,
                NormClipApplied: normClipApplied,
                PreClipDeltaNorm: preClip,
                PostClipDeltaNorm: postClip,
                CancelOutSuspected: cancelOut);

            return new PosNegLearningResult(delta, stats);
        }
    }
}
