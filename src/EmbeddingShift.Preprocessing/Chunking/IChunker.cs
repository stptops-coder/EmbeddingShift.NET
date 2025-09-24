namespace EmbeddingShift.Preprocessing.Chunking;

public interface IChunker
{
    IEnumerable<string> Chunk(string text);
}
