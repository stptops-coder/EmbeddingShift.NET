using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Providers.OpenAI;

/// <summary>
/// Dry-run provider: logs what would be sent to OpenAI (no network),
/// returns Sim embeddings (zero cost).
/// </summary>
public sealed class DryRunEmbeddingProvider : IEmbeddingProvider
{
    public string Name => "openai-dryrun";
    private readonly IEmbeddingProvider _inner;

    public DryRunEmbeddingProvider(IEmbeddingProvider inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var model = Environment.GetEnvironmentVariable("OPENAI_EMBED_MODEL") ?? "text-embedding-3-large";
        var apiBase = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://api.openai.com";
        var keySet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        Console.WriteLine($"[DRY-RUN] Would call {apiBase}/v1/embeddings | model={model} | textLen={text?.Length ?? 0} | keySet={keySet}");

        return await _inner.GetEmbeddingAsync(text ?? string.Empty);
    }
}

