using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Evaluators;
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
        public string Name { get; }

        private readonly EvaluationWorkflow _inner;
        private readonly IShift _shift;
        private readonly IReadOnlyList<ReadOnlyMemory<float>> _queries;
        private readonly IReadOnlyList<ReadOnlyMemory<float>> _references;
        private readonly string _dataset;

        public EvaluationWorkflowAdapter(
            string name,
            EvaluationWorkflow inner,
            IShift shift,
            IReadOnlyList<ReadOnlyMemory<float>> queries,
            IReadOnlyList<ReadOnlyMemory<float>> references,
            string dataset)
        {
            Name = name ?? "Evaluation";
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _shift = shift ?? throw new ArgumentNullException(nameof(shift));
            _queries = queries ?? throw new ArgumentNullException(nameof(queries));
            _references = references ?? throw new ArgumentNullException(nameof(references));
            _dataset = string.IsNullOrWhiteSpace(dataset) ? "dataset" : dataset;
        }

        public Task<WorkflowResult> RunAsync(IStatsCollector stats, CancellationToken ct = default)
        {
            EvaluationRunSummary summary;

            using (stats.TrackStep("Evaluate"))
            {
                // Important: capture structured metrics so the stats/run layer can persist and compare runs.
                summary = _inner.RunWithBaselineSummary(_shift, _queries, _references, _dataset);
            }

            var notes = $"Evaluation finished for dataset '{_dataset}'. RunId={summary.RunId}. ResultsPath={summary.ResultsPath}";

            return Task.FromResult(new WorkflowResult(
                Success: true,
                Metrics: summary.Metrics,
                Notes: notes
            ));
        }
    }
}
