using System;

namespace EmbeddingShift.Agentic;

/// <summary>
/// Small switch point for future model-backed reasoning clients.
/// The current repo state intentionally supports only local simulation.
/// </summary>
public static class AgenticReasoningClientFactory
{
    public const string BackendEnvironmentVariable = "EMBEDDINGSHIFT_AGENTIC_REASONING_BACKEND";

    public static IAgenticReasoningClient FromEnvironment()
        => FromKey(Environment.GetEnvironmentVariable(BackendEnvironmentVariable));

    public static IAgenticReasoningClient FromKey(string? backendKey)
    {
        var key = (backendKey ?? "sim").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key))
        {
            key = "sim";
        }

        return key switch
        {
            "sim" or "simulation" => new SimulatedAgenticReasoningClient(),

            "openai" or "chatgpt" => throw new NotSupportedException(
                "Agentic reasoning backend 'openai' is a deferred integration boundary and is not wired in this repo state. " +
                "Use 'sim' for local deterministic runs."),

            _ => throw new ArgumentOutOfRangeException(
                nameof(backendKey),
                backendKey,
                "Unsupported agentic reasoning backend. Supported now: sim. Deferred: openai.")
        };
    }
}
