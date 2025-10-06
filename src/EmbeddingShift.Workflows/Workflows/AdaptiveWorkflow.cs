using EmbeddingShift.Core.Shifts; // for NoShiftIngestBased
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
        private readonly ShiftMethod _method;


        public AdaptiveWorkflow(IShiftGenerator generator, ShiftEvaluationService service)
        {
            _generator = generator;
            _service = service;
        }

        // Backward-compatible overload: defaults to Method B (shifted)
        public AdaptiveWorkflow(IShiftGenerator generator, ShiftEvaluationService service, ShiftMethod method)
        {
            _generator = generator;
            _service = service;
            _method = method;
        }

        public IShift Run(ReadOnlyMemory<float> query, IReadOnlyList<ReadOnlyMemory<float>> references)
        {
            // NoShiftIngestBased → identity (no evaluation)
            if (_method == ShiftMethod.NoShiftIngestBased)
                return new NoShiftIngestBased();

            // Shifted → evaluate generator candidates
            var pairs = references.Select(r => (query, r)).ToList();
            var report = _service.Evaluate(pairs);
            return report.Results.First().BestShift!;
        }

    }
}
