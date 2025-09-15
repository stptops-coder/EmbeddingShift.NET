namespace EmbeddingShift.Adaptive;
using EmbeddingShift.Core.Shifts;
using EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Core.Generators;

public class AdaptiveShiftController {
  private readonly IShiftGenerator _generator; private readonly IShiftEvaluator _evaluator;
  public AdaptiveShiftController(IShiftGenerator generator, IShiftEvaluator evaluator) { _generator = generator; _evaluator = evaluator; }
  public IShift[] ProposeAndSelect((float[] Query, float[] Answer)[] pairs, float[][] before, float[][] after, int topK = 1) {
    var c = _generator.Generate(pairs);
    Array.Sort(c, (a,b) => _evaluator.Evaluate(b,before,after).CompareTo(_evaluator.Evaluate(a,before,after)));
    return c.Take(Math.Max(1, topK)).ToArray();
  }
}

