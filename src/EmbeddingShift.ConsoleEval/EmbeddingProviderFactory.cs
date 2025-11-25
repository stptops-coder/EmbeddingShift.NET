using System;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.ConsoleEval;

/// <summary>
/// Backend selector for embeddings. Central place to switch between
/// simulated embeddings and (later) real OpenAI-based embeddings.
///
/// Currently only the "sim" backend is supported. The "openai" branch
/// is intentionally not wired yet to avoid accidental real API usage.
/// </summary>
public enum EmbeddingBackend
{
    Sim,
    OpenAi
}

public static class EmbeddingProviderFactory
{
    /// <summary>
    /// Creates an embedding provider based on the EMBEDDING_BACKEND
    /// environment variable.
    ///
    /// - not set / unknown / "sim"  -> simulated embeddings
    /// - "openai"                   -> throws NotSupportedException for now
    /// </summary>
    public static IEmbeddingProvider FromEnvironment()
    {
        var backendEnv = Environment.GetEnvironmentVariable("EMBEDDING_BACKEND");

        var backend = backendEnv?.Trim().ToLowerInvariant() switch
        {
            "openai" => EmbeddingBackend.OpenAi,
            "sim"    => EmbeddingBackend.Sim,
            null     => EmbeddingBackend.Sim,
            ""       => EmbeddingBackend.Sim,
            _        => EmbeddingBackend.Sim
        };

        return Create(backend);
    }

    /// <summary>
    /// Creates an embedding provider for the specified backend.
    ///
    /// Sim is fully implemented (deterministic simulation by default).
    /// OpenAi is a placeholder and will be wired once real API usage
    /// is explicitly desired.
    /// </summary>
    public static IEmbeddingProvider Create(EmbeddingBackend backend)
        => backend switch
        {
            EmbeddingBackend.Sim => SimEmbeddingProvider.CreateDeterministic(),
            EmbeddingBackend.OpenAi => throw new NotSupportedException(
                "OpenAI backend wiring is not implemented yet. This is intentional to avoid accidental real API calls."
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unsupported embedding backend.")
        };
}
