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
        bool Debug,
        int HardNegTopK = 1);

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
        // Notes:
        // - This learner is optimized for small datasets (full scan over docEmbeddings per query).
        // - HardNegTopK allows learning from multiple top-ranked negatives to reduce cancel-out risk.
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
                if (emb.Length != dim) throw new ArgumentException($"Embedding dim mismatch for '{docId}'. Expected {dim}, got {emb.Length}.", nameof(docEmbeddings));
            }

            var sumDirection = new float[dim];

            var trainingCases = 0;
            var uniquePairs = new HashSet<string>(StringComparer.Ordinal);

            double sumDirNorm = 0;
            double minDirNorm = double.PositiveInfinity;
            double maxDirNorm = 0;
            var zeroDirs = 0;

            var k = options.HardNegTopK <= 0 ? 1 : options.HardNegTopK;

            for (var i = 0; i < queries.Count; i++)
            {
                var q = queries[i];

                if (!docEmbeddings.TryGetValue(q.RelevantDocId, out var posEmb))
                    throw new InvalidOperationException($"RelevantDocId '{q.RelevantDocId}' not found in docEmbeddings (query '{q.QueryId}').");

                var qEmb = await provider.GetEmbeddingAsync(q.Text).ConfigureAwait(false);

                // Score all docs
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

                // Take TopK hard negatives (excluding the positive doc)
                var taken = 0;
                for (var si = 0; si < scored.Count && taken < k; si++)
                {
                    var negDocId = scored[si].DocId;
                    if (string.Equals(negDocId, q.RelevantDocId, StringComparison.Ordinal))
                        continue;

                    if (!docEmbeddings.TryGetValue(negDocId, out var negEmb))
                        throw new InvalidOperationException($"NegDocId '{negDocId}' not found in docEmbeddings (query '{q.QueryId}').");

                    uniquePairs.Add($"{q.RelevantDocId}|{negDocId}");

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
                        // Do NOT consume the hard-negative budget on a zero-direction pair.
                        // Keep scanning deeper until we find a negative with usable direction magnitude,
                        // otherwise we can end up with cases=0 and delta=0 even though errors exist.
                        continue;
                    }

                    var dirNorm = Math.Sqrt(dirNormSq);
                    sumDirNorm += dirNorm;
                    minDirNorm = Math.Min(minDirNorm, dirNorm);
                    maxDirNorm = Math.Max(maxDirNorm, dirNorm);

                    trainingCases++;

                    if (options.Debug && debugLog is not null)
                    {
                        var posRank = posIndex + 1;
                        debugLog($"[PosNeg] Case {trainingCases}: q={q.QueryId}, pos={q.RelevantDocId}, neg={negDocId}, posRank={posRank}, negRank={si + 1}, |dir|={dirNorm:0.000000}");
                    }

                    taken++;
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

            // Heuristic: delta nearly zero despite cases => likely cancel-out.
            var cancelOut = trainingCases > 0 && preClip <= 1e-3;

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
