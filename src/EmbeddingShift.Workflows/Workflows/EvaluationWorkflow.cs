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
            _runner = runner;
        }

        public void Run(IShift shift,
            IReadOnlyList<ReadOnlyMemory<float>> queries, 
            IReadOnlyList<ReadOnlyMemory<float>> references, 
            string dataset)
        {
            _runner.RunEvaluation(shift, queries, references, dataset);
        }
    }
}
