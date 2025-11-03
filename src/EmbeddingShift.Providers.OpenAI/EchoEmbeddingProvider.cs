using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Providers.OpenAI;

/// <summary>
/// Echo-only provider: prints a safe preview of the would-be payload (no network),
/// returns Sim embeddings (zero cost).
/// </summary>
public sealed class EchoEmbeddingProvider : IEmbeddingProvider
{
    public string Name => "openai-echo";
    private readonly IEmbeddingProvider _inner;
    private readonly int _maxEchoChars;

    public EchoEmbeddingProvider(IEmbeddingProvider inner, int maxEchoChars = 280)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _maxEchoChars = Math.Max(32, maxEchoChars);
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        text ??= string.Empty;
        var shown = text.Length <= _maxEchoChars ? text : text[.._maxEchoChars] + "…";
        var model = Environment.GetEnvironmentVariable("OPENAI_EMBED_MODEL") ?? "text-embedding-3-large";
        var apiBase = Environment.GetEnvironmentVariable("OPENAI_BASE_URL") ?? "https://api.openai.com";
        var keySet = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

        Console.WriteLine($"[ECHO] model={model} len={text.Length} base={apiBase} keySet={keySet}");
        Console.WriteLine($"[ECHO] preview=\"{Escape(shown)}\"");

        return await _inner.GetEmbeddingAsync(text);
    }

    private static string Escape(string s) => s
        .Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\r", "\\r").Replace("\n", "\\n");
}

