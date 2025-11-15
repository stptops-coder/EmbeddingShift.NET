using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Core.Stats;

namespace EmbeddingShift.Core.Workflows
{
    public sealed record WorkflowResult(
        bool Success,
        IReadOnlyDictionary<string, double>? Metrics = null,
        string? Notes = null,
        System.Exception? Error = null
    );

    public interface IWorkflow
    {
        string Name { get; }
        Task<WorkflowResult> RunAsync(IStatsCollector stats, CancellationToken ct = default);
    }
}
