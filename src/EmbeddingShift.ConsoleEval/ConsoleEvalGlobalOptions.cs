using EmbeddingShift.Abstractions;

namespace EmbeddingShift.ConsoleEval;

public sealed record ConsoleEvalGlobalOptions
{
    // Wrapper/provider selection used by the console harness.
    // Base provider still comes from EmbeddingProviderFactory.FromEnvironment().
    public string Provider { get; init; } = "sim";

    // Optional base-backend override (maps to EMBEDDING_BACKEND).
    public string? Backend { get; init; } = null;

    // Used mainly by adaptive demo (Shifted vs identity).
    public ShiftMethod Method { get; init; } = ShiftMethod.Shifted;

    // Simulation tuning (maps to EMBEDDING_SIM_* env vars).
    public string? SimMode { get; init; } = null;                     // deterministic|noisy
    public string? SimNoiseAmplitude { get; init; } = null;            // float
    public string? SimAlgo { get; init; } = null;                      // sha256|semantic-hash
    public string? SimSemanticCharNGrams { get; init; } = null;        // 0|1

    // Semantic cache (maps to EMBEDDING_SEMANTIC_CACHE* env vars).
    public bool? SemanticCache { get; init; } = null;                  // on/off
    public string? CacheMax { get; init; } = null;                     // int
    public string? CacheHamming { get; init; } = null;                 // int
    public string? CacheApprox { get; init; } = null;                  // 0|1
}

public sealed record ConsoleEvalParsedArgs(ConsoleEvalGlobalOptions Options, string[] CommandArgs);
