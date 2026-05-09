using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Agentic.Console;

/// <summary>
/// Bundles the small agentic console entrypoints so the dispatcher remains
/// decoupled from the composition root.
/// </summary>
public sealed record AgenticConsoleServices(
    IWorkflow SimulationWorkflow);
