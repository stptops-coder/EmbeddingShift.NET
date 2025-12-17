namespace EmbeddingShift.Simulation
{
    /// <summary>
    /// Options for configuring the simulated embedding backend.
    ///
    /// This is a small, self-contained abstraction intended to be used by
    /// the simulation embedding client or factory. It does not perform any
    /// behavior by itself; it only describes how the simulation should behave.
    /// </summary>
    public sealed class EmbeddingSimulationOptions
    {
        /// <summary>
        /// Simulation mode. Default is Deterministic, which should behave
        /// exactly like the current simulation backend (no additional noise).
        /// </summary>
        public EmbeddingSimulationMode Mode { get; init; } = EmbeddingSimulationMode.Deterministic;

        /// <summary>
        /// Algorithm used to generate simulated embeddings.
        /// Default is Sha256 to preserve the legacy behavior unless explicitly changed.
        /// </summary>
        public EmbeddingSimulationAlgorithm Algorithm { get; init; } = EmbeddingSimulationAlgorithm.Sha256;

        /// <summary>
        /// When Algorithm = SemanticHash, this controls whether character n-gram features are added.
        /// Enabling this often improves robustness to punctuation/spacing changes, at the cost of a bit more compute.
        /// </summary>
        public bool SemanticIncludeCharacterNGrams { get; init; } = false;

        /// <summary>
        /// Amplitude of the additional noise that may be applied in Noisy mode.
        /// For deterministic mode, this value should be ignored by the backend.
        ///
        /// Typical values are small (e.g. 0.01 - 0.1), representing the maximum
        /// absolute deviation added per dimension.
        /// </summary>
        public float NoiseAmplitude { get; init; } = 0.0f;
    }
}
