namespace EmbeddingShift.Abstractions
{
    /// <summary>
    /// Produces text chunks from a source file (after parsing/normalization).
    /// Keep it simple: only Text + Order.
    /// </summary>
    public interface IIngestor
    {
        IEnumerable<(string Text, int Order)> Parse(string filePath);
    }
}
