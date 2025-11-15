using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Core.Stats;

namespace EmbeddingShift.Core.Workflows
{
    public sealed record RunArtifacts(
        Guid RunId,
        string RunName,
        string Workflow,
        string ReportMarkdown,
        IReadOnlyDictionary<string, double> Metrics,
        DateTimeOffset Started,
        DateTimeOffset Finished,
        bool Success,
        string? Notes,
        string? ErrorMessage
    );

    public sealed class StatsAwareWorkflowRunner
    {
        public async Task<RunArtifacts> ExecuteAsync(string runName, IWorkflow wf, CancellationToken ct = default)
        {
            var sink = new InMemoryStatsSink();
            var stats = new BasicStatsCollector(sink);

            var started = DateTimeOffset.UtcNow;
            stats.StartRun(runName, wf.Name);

            WorkflowResult result;
            try
            {
                result = await wf.RunAsync(stats, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                stats.RecordError("Run", ex, wf.Name);
                result = new WorkflowResult(false, null, null, ex);
            }
            finally
            {
                stats.EndRun(wf.Name);
            }

            var finished = DateTimeOffset.UtcNow;
            var md = StatsReport.ToMarkdown(sink.Events);

            var metrics = (result.Metrics ?? new Dictionary<string, double>())
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            return new RunArtifacts(
                RunId: stats.RunId,
                RunName: runName,
                Workflow: wf.Name,
                ReportMarkdown: md,
                Metrics: metrics,
                Started: started,
                Finished: finished,
                Success: result.Success,
                Notes: result.Notes,
                ErrorMessage: result.Error?.Message
            );
        }
    }
}
