namespace EmbeddingShift.Simulation;

/// <summary>
/// Algorithms used to generate simulated embeddings.
/// </summary>
public enum EmbeddingSimulationAlgorithm
{
    /// <summary>
    /// Legacy behavior: generate a dense vector from a SHA-256 hash of the full input text.
    /// Deterministic, but it does not preserve semantic locality (nearby texts look unrelated).
    /// </summary>
    Sha256 = 0,

    /// <summary>
    /// Feature-hash tokens (and optionally character n-grams) into a dense vector and L2-normalize.
    /// This preserves a basic notion of semantic locality and is useful for realistic tests.
    /// </summary>
    SemanticHash = 1
}
