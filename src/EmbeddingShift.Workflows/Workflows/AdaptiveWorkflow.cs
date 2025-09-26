using EmbeddingShift.Abstractions;
using EmbeddingShift.Adaptive;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// Adaptive workflow: generates candidate shifts and selects best shift.
    /// </summary>
    public sealed class AdaptiveWorkflow
    {
        private readonly IShiftGenerator _generator;
        private readonly ShiftEvaluationService _service;

        public AdaptiveWorkflow(IShiftGenerator generator, ShiftEvaluationService service)
        {
            _generator = generator;
            _service = service;
        }

        public IShift Run(ReadOnlyMemory<float> query, IReadOnlyList<ReadOnlyMemory<float>> references)
        {
            var pairs = references.Select(r => (query, r)).ToList();
            var report = _service.Evaluate(pairs);
            return report.Results.First().BestShift!;
        }
    }
}
