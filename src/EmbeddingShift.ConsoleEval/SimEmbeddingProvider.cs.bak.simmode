using System.Security.Cryptography;
using System.Text;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.ConsoleEval;

public sealed class SimEmbeddingProvider : IEmbeddingProvider
{
    public string Name => "sim";
    private const int Dim = 1536;

    public Task<float[]> GetEmbeddingAsync(string text)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
        var vec = new float[Dim];
        for (int i = 0; i < Dim; i++)
            vec[i] = (bytes[i % bytes.Length] / 255f) - 0.5f;
        return Task.FromResult(vec);
    }
}
