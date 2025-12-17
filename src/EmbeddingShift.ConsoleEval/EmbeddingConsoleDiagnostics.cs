using System;

namespace EmbeddingShift.ConsoleEval;

public static class EmbeddingConsoleDiagnostics
{
    public static void PrintEmbeddingConfiguration()
    {
        var backend = (Environment.GetEnvironmentVariable("EMBEDDING_BACKEND") ?? "sim").Trim();
        if (string.IsNullOrWhiteSpace(backend)) backend = "sim";

        var simMode = (Environment.GetEnvironmentVariable("EMBEDDING_SIM_MODE") ?? "deterministic").Trim();
        if (string.IsNullOrWhiteSpace(simMode)) simMode = "deterministic";

        var simNoise = (Environment.GetEnvironmentVariable("EMBEDDING_SIM_NOISE_AMPLITUDE") ?? "0").Trim();
        if (string.IsNullOrWhiteSpace(simNoise)) simNoise = "0";

        var simAlgo = (Environment.GetEnvironmentVariable("EMBEDDING_SIM_ALGO") ?? "sha256").Trim();
        if (string.IsNullOrWhiteSpace(simAlgo)) simAlgo = "sha256";

        var semanticCache = (Environment.GetEnvironmentVariable("EMBEDDING_SEMANTIC_CACHE") ?? "0").Trim();
        if (string.IsNullOrWhiteSpace(semanticCache)) semanticCache = "0";

        Console.WriteLine(
            $"[Embedding] Backend={backend}, SimMode={simMode}, Algo={simAlgo}, NoiseAmplitude={simNoise}, SemanticCache={semanticCache}");
    }
}
