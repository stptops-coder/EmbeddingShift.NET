using System;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Embeddings;

namespace EmbeddingShift.ConsoleEval;

public enum EmbeddingBackend
{
    Sim,
    OpenAi
}

public static class EmbeddingProviderFactory
{
    public static IEmbeddingProvider FromEnvironment()
    {
        var backendEnv = Environment.GetEnvironmentVariable("EMBEDDING_BACKEND");
        var backendKey = (backendEnv ?? "sim").Trim();
        if (string.IsNullOrWhiteSpace(backendKey)) backendKey = "sim";

        var backend = backendKey.ToLowerInvariant() switch
        {
            "openai" => EmbeddingBackend.OpenAi,
            "sim" => EmbeddingBackend.Sim,
            _ => EmbeddingBackend.Sim
        };

        IEmbeddingProvider provider = Create(backend);

        // Optional semantic cache decorator (opt-in).
        if (SemanticCacheOptions.IsTruthy(Environment.GetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE")))
        {
            provider = new SemanticCacheEmbeddingProvider(provider, SemanticCacheOptions.FromEnvironment());
        }

        return provider;
    }

    public static IEmbeddingProvider Create(EmbeddingBackend backend)
        => backend switch
        {
            EmbeddingBackend.Sim => new SimEmbeddingProvider(),

            EmbeddingBackend.OpenAi => throw new NotSupportedException(
                "OpenAI embedding backend is a deferred integration boundary in this repo state. " +
                "Use the simulated backend for the verified local path."
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unsupported embedding backend.")
        };
}
