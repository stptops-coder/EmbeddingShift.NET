using EmbeddingShift.Abstractions;
using System;
using System.Collections.Generic;

namespace EmbeddingShift.Abstractions
{
    public interface IShiftGenerator
    {
        IEnumerable<IShift> Generate(
            IReadOnlyList<(ReadOnlyMemory<float> Query, ReadOnlyMemory<float> Answer)> pairs);
    }
}
