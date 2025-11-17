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
    /// Mini preprocessing workflow:
    /// - in-memory "documents" (short insurance-like texts)
    /// - text cleanup / normalization
    /// - simple character-based chunking
    /// - collects basic statistics as metrics
    /// </summary>
    public sealed class MiniPreprocessingWorkflow : IWorkflow
    {
        public string Name => "Mini-Preprocessing";

        public Task<WorkflowResult> RunAsync(IStatsCollector stats, CancellationToken ct = default)
        {
            // 1) In-memory documents (later this can be replaced by real files / domains)
            var docs = new Dictionary<string, string>
            {
                ["policy-1"] =
                    "This is a sample insurance policy. It covers fire and water damage.\n" +
                    "Exclusions apply for intentional damage and war-related incidents.",
                ["policy-2"] =
                    "Another policy document. It focuses on theft coverage and how claims " +
                    "must be reported within a certain time window.",
                ["policy-3"] =
                    "A short policy about flood and storm damage. Some exclusions may apply."
            };

            int totalDocs         = 0;
            int totalChunks       = 0;
            int totalChars        = 0;
            double totalWsRatio   = 0.0;

            const int maxChunkLength = 80;

            using (stats.TrackStep("Mini-Preprocessing"))
            {
                foreach (var kvp in docs)
                {
                    ct.ThrowIfCancellationRequested();

                    totalDocs++;

                    var cleaned = CleanupText(kvp.Value);
                    var chunks  = ChunkByLength(cleaned, maxChunkLength);

                    foreach (var chunk in chunks)
                    {
                        totalChunks++;
                        totalChars += chunk.Length;

                        if (chunk.Length > 0)
                        {
                            var wsCount = chunk.Count(char.IsWhiteSpace);
                            totalWsRatio += (double)wsCount / chunk.Length;
                        }
                    }
                }
            }

            double avgChunkLength = totalChunks == 0 ? 0.0 : (double)totalChars / totalChunks;
            double avgWsRatio     = totalChunks == 0 ? 0.0 : totalWsRatio / totalChunks;

            var metrics = new Dictionary<string, double>
            {
                ["prep.totalDocs"]       = totalDocs,
                ["prep.totalChunks"]     = totalChunks,
                ["prep.avgChunkLength"]  = avgChunkLength,
                ["prep.avgWhitespace"]   = avgWsRatio
            };

            var notes =
                "Mini preprocessing workflow over in-memory policy texts " +
                "(cleanup + simple chunking + basic statistics).";

            return Task.FromResult(new WorkflowResult(
                Success: true,
                Metrics: metrics,
                Notes: notes));
        }

        /// <summary>
        /// Very small, conservative cleanup:
        /// - normalize line breaks to spaces
        /// - collapse multiple whitespace characters into a single space
        /// - trim start/end
        /// - lower-case for consistency
        /// </summary>
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

        /// <summary>
        /// Simple character-based chunking helper (no overlap, fixed maximum length).
        /// Intentionally kept simple; can later be replaced by SimpleTextChunker.
        /// </summary>
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
    }
}
