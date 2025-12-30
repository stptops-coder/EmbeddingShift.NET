using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Evaluators;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// Evaluation workflow: benchmarks shifts on datasets and logs results.
    /// </summary>
    public sealed class EvaluationWorkflow
    {
        private readonly EvaluationRunner _runner;

        public EvaluationWorkflow(EvaluationRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public void Run(
            IShift shift,
            IReadOnlyList<ReadOnlyMemory<float>> queries,
            IReadOnlyList<ReadOnlyMemory<float>> references,
            string dataset)
        {
            _runner.RunEvaluation(shift, queries, references, dataset);
        }

        /// <summary>
        /// Runs evaluation and returns a structured summary (UI/automation friendly).
        /// </summary>
        public EvaluationRunSummary RunWithSummary(
            IShift shift,
            IReadOnlyList<ReadOnlyMemory<float>> queries,
            IReadOnlyList<ReadOnlyMemory<float>> references,
            string dataset)
        {
            return _runner.RunEvaluationSummary(shift, queries, references, dataset);
        }
    }
}
