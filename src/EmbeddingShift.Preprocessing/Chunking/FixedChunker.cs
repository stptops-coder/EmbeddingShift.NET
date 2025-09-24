using System.Text;

namespace EmbeddingShift.Preprocessing.Chunking;

/// Fixed-size chunker with optional overlap (by characters).
public sealed class FixedChunker : IChunker
{
    private readonly int _size;
    private readonly int _overlap;

    public FixedChunker(int size = 1000, int overlap = 100)
    {
        _size = size > 0 ? size : 1000;
        _overlap = overlap >= 0 ? overlap : 0;
        if (_overlap >= _size) _overlap = 0;
    }

    public IEnumerable<string> Chunk(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        var i = 0;
        while (i < text.Length)
        {
            var end = Math.Min(i + _size, text.Length);
            yield return text.Substring(i, end - i);
            i = end - _overlap;
            if (i < 0) i = 0;
            if (i >= text.Length) yield break;
        }
    }
}
