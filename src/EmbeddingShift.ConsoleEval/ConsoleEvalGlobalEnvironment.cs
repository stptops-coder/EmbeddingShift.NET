using System;

namespace EmbeddingShift.ConsoleEval;

public static class ConsoleEvalGlobalEnvironment
{
    public static void Apply(ConsoleEvalGlobalOptions o)
    {
        if (o is null) return;

        if (!string.IsNullOrWhiteSpace(o.Backend))
            Environment.SetEnvironmentVariable("EMBEDDING_BACKEND", o.Backend);

        if (!string.IsNullOrWhiteSpace(o.SimMode))
            Environment.SetEnvironmentVariable("EMBEDDING_SIM_MODE", o.SimMode);

        if (!string.IsNullOrWhiteSpace(o.SimNoiseAmplitude))
            Environment.SetEnvironmentVariable("EMBEDDING_SIM_NOISE_AMPLITUDE", o.SimNoiseAmplitude);

        if (!string.IsNullOrWhiteSpace(o.SimAlgo))
            Environment.SetEnvironmentVariable("EMBEDDING_SIM_ALGO", o.SimAlgo);

        if (!string.IsNullOrWhiteSpace(o.SimSemanticCharNGrams))
            Environment.SetEnvironmentVariable("EMBEDDING_SIM_SEMANTIC_CHAR_NGRAMS", o.SimSemanticCharNGrams);

        if (o.SemanticCache.HasValue)
            Environment.SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE", o.SemanticCache.Value ? "1" : "0");

        if (!string.IsNullOrWhiteSpace(o.CacheMax))
            Environment.SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE_MAX", o.CacheMax);

        if (!string.IsNullOrWhiteSpace(o.CacheHamming))
            Environment.SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE_HAMMING", o.CacheHamming);

        if (!string.IsNullOrWhiteSpace(o.CacheApprox))
            Environment.SetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE_APPROX", o.CacheApprox);
    }
}
