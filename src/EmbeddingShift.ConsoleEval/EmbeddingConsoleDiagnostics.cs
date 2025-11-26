using System;

namespace EmbeddingShift.ConsoleEval;

/// <summary>
/// Small helper to print the current embedding configuration to the console.
/// This is useful when experimenting with different backends / simulation modes.
/// </summary>
internal static class EmbeddingConsoleDiagnostics
{
    public static void PrintEmbeddingConfiguration()
    {
        var backend = (Environment.GetEnvironmentVariable("EMBEDDING_BACKEND") ?? "sim").Trim();
        if (string.IsNullOrWhiteSpace(backend))
        {
            backend = "sim";
        }

        var simMode = (Environment.GetEnvironmentVariable("EMBEDDING_SIM_MODE") ?? "deterministic").Trim();
        if (string.IsNullOrWhiteSpace(simMode))
        {
            simMode = "deterministic";
        }

        var simNoise = (Environment.GetEnvironmentVariable("EMBEDDING_SIM_NOISE_AMPLITUDE") ?? "0").Trim();
        if (string.IsNullOrWhiteSpace(simNoise))
        {
            simNoise = "0";
        }

        Console.WriteLine($"[Embedding] Backend={backend}, SimMode={simMode}, NoiseAmplitude={simNoise}");
    }
}
