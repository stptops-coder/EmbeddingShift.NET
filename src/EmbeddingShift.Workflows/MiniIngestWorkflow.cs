using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Core.Stats;
using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// Mini ingest workflow:
    /// - in-memory "documents" (short insurance-like texts)
    /// - internal cleanup + chunking
    /// - simulated embeddings (deterministic, hash-based)
    /// - basic ingest statistics as metrics
    /// </summary>
    public sealed class MiniIngestWorkflow : IWorkflow
    {
        public string Name => "Mini-Ingest";

        public Task<WorkflowResult> RunAsync(IStatsCollector stats, CancellationToken ct = default)
        {
            var docs = new Dictionary<string, string>
            {
                ["policy-1"] =
                    "This is a sample insurance policy. It covers fire and water damage.\n" +
                    "Exclusions apply for intentional damage and war-related incidents.",
                ["policy-2"] =
                    "Another policy document. It focuses on theft coverage and on how claims " +
                    "must be reported within a certain time window.",
                ["policy-3"] =
                    "A short policy about flood and storm damage. Some exclusions may apply."
            };

            const int maxChunkLength = 80;
            const int embeddingDim   = 16;

            var ingested = new List<IngestRecord>();
            var embeddingProvider = new MiniEmbeddingProvider(embeddingDim);

            using (stats.TrackStep("Mini-Ingest"))
            {
                foreach (var kvp in docs)
                {
                    ct.ThrowIfCancellationRequested();

                    var docId = kvp.Key;
                    var raw   = kvp.Value;

                    var cleaned = CleanupText(raw);
                    var chunks  = ChunkByLength(cleaned, maxChunkLength);

                    int chunkIndex = 0;

                    foreach (var chunk in chunks)
                    {
                        var emb = embeddingProvider.CreateEmbedding(docId, chunkIndex, chunk);
                        ingested.Add(new IngestRecord(docId, chunkIndex, chunk, emb));
                        chunkIndex++;
                    }
                }
            }

            // basic ingest statistics
            int totalDocs     = docs.Count;
            int totalChunks   = ingested.Count;
            double avgDim     = embeddingDim;
            double avgNorm    = 0.0;

            if (totalChunks > 0)
            {
                avgNorm = ingested
                    .Select(r => L2Norm(r.Embedding))
                    .Average();
            }

            var metrics = new Dictionary<string, double>
            {
                ["ingest.totalDocs"]       = totalDocs,
                ["ingest.totalChunks"]     = totalChunks,
                ["ingest.embeddingDim"]    = avgDim,
                ["ingest.avgEmbeddingNorm"] = avgNorm
            };

            var notes =
                "Mini ingest workflow over in-memory policy texts " +
                "(cleanup + chunking + simulated embeddings + ingest statistics).";

            return Task.FromResult(new WorkflowResult(
                Success: true,
                Metrics: metrics,
                Notes: notes));
        }

        private static string CleanupText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            normalized = normalized.Replace('\n', ' ');

            var sb = new StringBuilder(normalized.Length);
            bool lastWasWs = false;

            foreach (var ch in normalized)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!lastWasWs)
                    {
                        sb.Append(' ');
                        lastWasWs = true;
                    }
                }
                else
                {
                    sb.Append(char.ToLowerInvariant(ch));
                    lastWasWs = false;
                }
            }

            return sb.ToString().Trim();
        }

        private static IEnumerable<string> ChunkByLength(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text) || maxLen <= 0)
                yield break;

            for (int i = 0; i < text.Length; i += maxLen)
            {
                var len = Math.Min(maxLen, text.Length - i);
                yield return text.Substring(i, len);
            }
        }

        private static double L2Norm(float[] v)
        {
            double sum = 0.0;
            for (int i = 0; i < v.Length; i++)
            {
                sum += v[i] * v[i];
            }
            return Math.Sqrt(sum);
        }

        private sealed record IngestRecord(
            string DocId,
            int ChunkIndex,
            string ChunkText,
            float[] Embedding);

        /// <summary>
        /// Deterministic, hash-based mini embedding generator.
        /// This is only for simulation; it does not aim to model real semantics.
        /// </summary>
        private sealed class MiniEmbeddingProvider
        {
            private readonly int _dimension;

            public MiniEmbeddingProvider(int dimension)
            {
                if (dimension <= 0) throw new ArgumentOutOfRangeException(nameof(dimension));
                _dimension = dimension;
            }

            public float[] CreateEmbedding(string docId, int chunkIndex, string text)
            {
                var seed = ComputeSeed(docId, chunkIndex, text);
                var vector = new float[_dimension];

                var rng = new Random(seed);
                for (int i = 0; i < _dimension; i++)
                {
                    // small, centered values around 0
                    vector[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
                }

                return vector;
            }

            private static int ComputeSeed(string docId, int chunkIndex, string text)
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + (docId?.GetHashCode() ?? 0);
                    hash = hash * 31 + chunkIndex.GetHashCode();
                    hash = hash * 31 + (text?.GetHashCode() ?? 0);
                    return hash;
                }
            }
        }
    }
}
