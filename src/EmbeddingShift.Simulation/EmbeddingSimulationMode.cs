namespace EmbeddingShift.Simulation
{
    /// <summary>
    /// Simulation mode for embedding generation.
    ///
    /// Deterministic:
    ///   - Same input, same embedding, no stochastic noise.
    ///
    /// Noisy:
    ///   - Same base behavior, but with an additional random noise component,
    ///     intended to approximate non-deterministic backends.
    /// </summary>
    public enum EmbeddingSimulationMode
    {
        Deterministic = 0,
        Noisy = 1
    }
}
