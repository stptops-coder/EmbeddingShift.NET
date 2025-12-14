namespace EmbeddingShift.Core.Training;

/// <summary>
/// Defines the operational mode of a training run.
/// Micro: small datasets, debug-oriented, diagnostics-first.
/// Production: large datasets, promotion-eligible, gated.
/// </summary>
public enum TrainingMode
{
    Micro,
    Production
}
