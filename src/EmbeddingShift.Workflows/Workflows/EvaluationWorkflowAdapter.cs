using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Stats;
using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// Adapter that exposes EvaluationWorkflow as an IWorkflow and integrates
    /// with the stats layer. It does not change EvaluationWorkflow itself.
    /// </summary>
    public sealed class EvaluationWorkflowAdapter : IWorkflow
    {
        private readonly EvaluationWorkflow _inner;
        private readonly IShift _shift;
        private readonly IReadOnlyList<ReadOnlyMemory<float>> _queries;
        private readonly IReadOnlyList<ReadOnlyMemory<float>> _references;
        private readonly string _dataset;

        public string Name => "Evaluation";

        public EvaluationWorkflowAdapter(
            EvaluationWorkflow inner,
            IShift shift,
            IReadOnlyList<ReadOnlyMemory<float>> queries,
            IReadOnlyList<ReadOnlyMemory<float>> references,
            string dataset)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _shift = shift ?? throw new ArgumentNullException(nameof(shift));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _references = references ?? throw new ArgumentNullException(nameof(references));
            _dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
        }

        public Task<WorkflowResult> RunAsync(IStatsCollector stats, CancellationToken ct = default)
        {
            // For now we only wrap the existing synchronous evaluation call
            using (stats.TrackStep("Evaluate"))
            {
                _inner.Run(_shift, _queries, _references, _dataset);
            }

            // Later we can enrich metrics here (e.g. nDCG, MRR) once they are exposed
            IReadOnlyDictionary<string, double>? metrics = null;

            return Task.FromResult(new WorkflowResult(
                Success: true,
                Metrics: metrics,
                Notes: $"Evaluation finished for dataset '{_dataset}'."
            ));
        }
    }
}
