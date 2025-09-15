namespace EmbeddingShift.Core.Evaluators;
using EmbeddingShift.Abstractions;
public interface IShiftEvaluator { double Evaluate(IShift shift, float[][] samplesBefore, float[][] samplesAfter); }

