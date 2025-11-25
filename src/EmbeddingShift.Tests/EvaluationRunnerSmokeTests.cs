using System;
using System.Collections.Generic;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Core.Shifts;
using Xunit;

namespace EmbeddingShift.Tests
{
    public class EvaluationRunnerSmokeTests
    {
        private sealed class NullLogger : IRunLogger
        {
            public Guid StartRun(string kind, string dataset) => Guid.NewGuid();
            public void LogMetric(Guid runId, string name, double value) { /* no-op */ }
            public void CompleteRun(Guid runId, string artifactPath) { /* no-op */ }
        }

        private sealed class DummyEvaluator : IShiftEvaluator
        {
            public EvaluationResult Evaluate(
                IShift shift,
                ReadOnlySpan<float> query,
                IReadOnlyList<ReadOnlyMemory<float>> refs)
            {
                // constant score for smoke test; use constructor signature (name, score, notes)
                return new EvaluationResult(
                    shift?.Name ?? "DummyShift",
                    1.0,
                    "smoke");
            }
        }


        [Fact]
        public void RunEvaluation_DoesNotThrow()
        {
            var logger = new NullLogger();

            // Prefer defaults if available in your codebase:
            // var runner = EvaluationRunner.WithDefaults(logger);
            // Fallback: construct with one dummy evaluator
            var runner = new EvaluationRunner(new IShiftEvaluator[] { new DummyEvaluator() }, logger);

            var q = new List<ReadOnlyMemory<float>> { new float[] { 0, 1, 2 }.AsMemory() };
            var r = new List<ReadOnlyMemory<float>> { new float[] { 2, 1, 0 }.AsMemory() };
            var shift = new NoShiftIngestBased();

            runner.RunEvaluation(shift, q, r, "SmokeDataset");
        }
    }
}