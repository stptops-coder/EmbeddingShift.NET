using System;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.ConsoleEval;

/// <summary>
/// Backend selector for embeddings. Central place to switch between
/// simulated embeddings and (later) real OpenAI-based embeddings.
///
/// Currently only the "sim" backend is implemented. The "openai" branch
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
    /// - not set / unknown / "sim"  -> simulated embeddings (SimEmbeddingProvider)
    /// - "openai"                   -> throws NotSupportedException for now
    /// </summary>
    public static IEmbeddingProvider FromEnvironment()
    {
        var backendEnv = Environment.GetEnvironmentVariable("EMBEDDING_BACKEND");

        var backendKey = (backendEnv ?? "sim").Trim();
        if (string.IsNullOrWhiteSpace(backendKey))
        {
            backendKey = "sim";
        }

        var backend = backendKey.ToLowerInvariant() switch
        {
            "openai" => EmbeddingBackend.OpenAi,
            "sim"    => EmbeddingBackend.Sim,
            _        => EmbeddingBackend.Sim
        };

        return Create(backend);
    }

    /// <summary>
    /// Creates an embedding provider for the specified backend.
    ///
    /// Sim is fully implemented and will internally look at the simulation
    /// environment variables (EMBEDDING_SIM_MODE, EMBEDDING_SIM_NOISE_AMPLITUDE)
    /// via SimEmbeddingProvider.
    ///
    /// OpenAi is a placeholder and will be wired once real API usage
    /// is explicitly desired.
    /// </summary>
    public static IEmbeddingProvider Create(EmbeddingBackend backend)
        => backend switch
        {
            // Parameterless SimEmbeddingProvider → uses CreateOptionsFromEnvironment()
            EmbeddingBackend.Sim => new SimEmbeddingProvider(),

            EmbeddingBackend.OpenAi => throw new NotSupportedException(
                "OpenAI embedding backend is not wired yet. " +
                "Once the OpenAI provider is implemented, this branch of " +
                "EmbeddingProviderFactory will construct it."
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, "Unsupported embedding backend.")
        };
}
