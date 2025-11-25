using System;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.ConsoleEval
{
    /// <summary>
    /// Central factory for creating the embedding provider used by ConsoleEval.
    /// For now, only the "sim" backend is supported. The "openai" branch is a
    /// placeholder that we can wire up later once the OpenAI provider is ready.
    /// </summary>
    internal static class EmbeddingProviderFactory
    {
        /// <summary>
        /// Creates an IEmbeddingProvider based on the EMBEDDING_BACKEND
        /// environment variable (currently: "sim" or "openai").
        /// 
        /// Defaults to "sim" if the variable is not set or unrecognized.
        /// </summary>
        public static IEmbeddingProvider CreateFromEnvironment()
        {
            var backend = (Environment.GetEnvironmentVariable("EMBEDDING_BACKEND") ?? "sim")
                .Trim()
                .ToLowerInvariant();

            return backend switch
            {
                "" or "sim"       => CreateSim(),
                "openai"          => CreateOpenAiPlaceholder(),
                _                 => CreateSim()
            };
        }

        /// <summary>
        /// Explicitly creates the simulation backend (SimEmbeddingProvider).
        /// </summary>
        public static IEmbeddingProvider CreateSim()
            => new SimEmbeddingProvider();

        /// <summary>
        /// Placeholder for a future OpenAI backend.
        /// We keep this explicit so that the switch logic is already in place,
        /// but do not accidentally trigger real API calls yet.
        /// </summary>
        private static IEmbeddingProvider CreateOpenAiPlaceholder()
        {
            throw new NotSupportedException(
                "The OpenAI embedding backend is not wired yet. " +
                "Once the OpenAI provider is implemented, this branch of " +
                "EmbeddingProviderFactory will construct it.");
        }
    }
}
