using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Abstractions;

public interface IShiftGenerator
{
    /// <summary>
    /// Erzeugt 0..n neue Shifts aus Query/Referenzen (z.B. Delta-Vektor, gewichtete Achsen, …).
    /// </summary>
    IEnumerable<IShift> Generate(
        ReadOnlySpan<float> query,
        IReadOnlyList<ReadOnlyMemory<float>> referenceEmbeddings
    );
}
