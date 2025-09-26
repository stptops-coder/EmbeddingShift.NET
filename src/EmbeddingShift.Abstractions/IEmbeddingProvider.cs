namespace EmbeddingShift.Abstractions
{
    public interface IEmbeddingProvider
    {
        string Name { get; }
        Task<float[]> GetEmbeddingAsync(string text);
    }
}
