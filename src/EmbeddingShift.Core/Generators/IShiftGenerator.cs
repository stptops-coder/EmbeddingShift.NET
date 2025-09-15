namespace EmbeddingShift.Core.Generators;
using EmbeddingShift.Core.Shifts;
public interface IShiftGenerator { IShift[] Generate((float[] Query, float[] Answer)[] pairs); }

