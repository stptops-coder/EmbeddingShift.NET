namespace EmbeddingShift.Core.Generators;
using EmbeddingShift.Abstractions;
public interface IShiftGenerator { IShift[] Generate((float[] Query, float[] Answer)[] pairs); }

