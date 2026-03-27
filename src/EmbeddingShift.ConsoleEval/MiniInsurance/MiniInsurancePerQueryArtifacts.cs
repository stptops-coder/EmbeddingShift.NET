using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.ConsoleEval.MiniInsurance
{
    /// <summary>
    /// Thin compatibility shim. The actual per-query artifact writer is shared at the ConsoleEval level.
    /// </summary>
    internal static class MiniInsurancePerQueryArtifacts
    {
        public const string FileName = PerQueryArtifactWriter.FileName;

        public static void TryPersist(string? runDir, IWorkflow workflow)
            => PerQueryArtifactWriter.TryPersist(runDir, workflow);
    }
}
