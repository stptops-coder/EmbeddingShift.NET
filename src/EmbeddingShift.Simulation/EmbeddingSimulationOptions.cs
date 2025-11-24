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
        /// Amplitude of the additional noise that may be applied in Noisy mode.
        /// For deterministic mode, this value should be ignored by the backend.
        ///
        /// Typical values are small (e.g. 0.01 - 0.1), representing the maximum
        /// absolute deviation added per dimension.
        /// </summary>
        public float NoiseAmplitude { get; init; } = 0.0f;
    }
}
