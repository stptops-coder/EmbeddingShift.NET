namespace EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Core.Shifts;
public interface IShiftEvaluator { double Evaluate(IShift shift, float[][] samplesBefore, float[][] samplesAfter); }

