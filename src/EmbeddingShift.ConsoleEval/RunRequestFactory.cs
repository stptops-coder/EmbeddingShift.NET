using System;
using System.Collections.Generic;
using EmbeddingShift.Core.Runs;

namespace EmbeddingShift.ConsoleEval;

internal static class RunRequestFactory
{
    private static readonly string[] SnapshotKeys =
    [
        "EMBEDDINGSHIFT_ROOT",
        "EMBEDDINGSHIFT_TENANT",
        "EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT",
        "EMBEDDING_BACKEND",
        "EMBEDDING_SIM_MODE",
        "EMBEDDING_SIM_NOISE_AMPLITUDE",
        "EMBEDDING_SIM_ALGO",
        "EMBEDDING_SIM_SEMANTIC_CHAR_NGRAMS",
        "EMBEDDING_SEMANTIC_CACHE",
        "EMBEDDING_SEMANTIC_CACHE_MAX",
        "EMBEDDING_SEMANTIC_CACHE_HAMMING",
        "EMBEDDING_SEMANTIC_CACHE_APPROX"
    ];

    public static RunRequest Create(string[] commandArgs, string? notes = null)
    {
        if (commandArgs is null) throw new ArgumentNullException(nameof(commandArgs));

        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in SnapshotKeys)
        {
            var v = Environment.GetEnvironmentVariable(k);
            if (!string.IsNullOrWhiteSpace(v))
                env[k] = v;
        }

        var global = new List<string>(capacity: 16);

        if (env.TryGetValue("EMBEDDINGSHIFT_TENANT", out var tenant) && !string.IsNullOrWhiteSpace(tenant))
            global.Add($"--tenant={tenant}");

        if (env.TryGetValue("EMBEDDING_BACKEND", out var backend) && !string.IsNullOrWhiteSpace(backend))
            global.Add($"--backend={backend}");

        if (env.TryGetValue("EMBEDDING_SIM_MODE", out var simMode) && !string.IsNullOrWhiteSpace(simMode))
            global.Add($"--sim-mode={simMode}");

        if (env.TryGetValue("EMBEDDING_SIM_NOISE_AMPLITUDE", out var noise) && !string.IsNullOrWhiteSpace(noise))
            global.Add($"--sim-noise={noise}");

        if (env.TryGetValue("EMBEDDING_SIM_ALGO", out var algo) && !string.IsNullOrWhiteSpace(algo))
            global.Add($"--sim-algo={algo}");

        if (env.TryGetValue("EMBEDDING_SIM_SEMANTIC_CHAR_NGRAMS", out var ngrams) && !string.IsNullOrWhiteSpace(ngrams))
            global.Add($"--sim-char-ngrams={ngrams}");

        if (env.TryGetValue("EMBEDDING_SEMANTIC_CACHE", out var cache) && !string.IsNullOrWhiteSpace(cache))
        {
            var on = cache.Trim().Equals("1", StringComparison.OrdinalIgnoreCase) ||
                     cache.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

            global.Add(on ? "--semantic-cache" : "--no-semantic-cache");
        }

        if (env.TryGetValue("EMBEDDING_SEMANTIC_CACHE_MAX", out var cacheMax) && !string.IsNullOrWhiteSpace(cacheMax))
            global.Add($"--cache-max={cacheMax}");

        if (env.TryGetValue("EMBEDDING_SEMANTIC_CACHE_HAMMING", out var cacheHam) && !string.IsNullOrWhiteSpace(cacheHam))
            global.Add($"--cache-hamming={cacheHam}");

        if (env.TryGetValue("EMBEDDING_SEMANTIC_CACHE_APPROX", out var cacheApprox) && !string.IsNullOrWhiteSpace(cacheApprox))
            global.Add($"--cache-approx={cacheApprox}");

        return new RunRequest(
            GlobalArgs: global.ToArray(),
            CommandArgs: commandArgs,
            EnvironmentSnapshot: env,
            Notes: notes);
    }
}
