using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Core.Stats;

namespace EmbeddingShift.Core.Workflows
{
    /// <summary>
    /// Simple demo workflow:
    /// - Simulates ingest, chunk, embedding, evaluation steps
    /// - Records timings and a few example metrics
    /// </summary>
    public sealed class SmokeStatsWorkflow : IWorkflow
    {
        public string Name => "SmokeStats";

        public async Task<WorkflowResult> RunAsync(IStatsCollector stats, CancellationToken ct = default)
        {
            var metrics = new Dictionary<string, double>();

            using (stats.TrackStep("Ingest"))
            {
                await Task.Delay(50, ct);
                metrics["documents_ingested"] = 42;
                stats.RecordMetric("documents_ingested", 42);
            }

            using (stats.TrackStep("Chunk"))
            {
                await Task.Delay(30, ct);
                metrics["chunks"] = 170;
                stats.RecordMetric("chunks", 170);
            }

            using (stats.TrackStep("Embedding"))
            {
                await Task.Delay(60, ct);
                stats.RecordExternal("embedding", tokensIn: 900, tokensOut: 0);
            }

            using (stats.TrackStep("Evaluate"))
            {
                await Task.Delay(40, ct);
                metrics["nDCG@10"] = 0.62;
                metrics["MRR"] = 0.38;
                stats.RecordMetric("nDCG@10", 0.62);
                stats.RecordMetric("MRR", 0.38);
            }

            return new WorkflowResult(
                Success: true,
                Metrics: metrics,
                Notes: "SmokeStats workflow completed.");
        }
    }
}
