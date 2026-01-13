using System.IO;
using System.Text;
using System.Text.Json;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;

namespace EmbeddingShift.ConsoleEval.MiniInsurance
{
    internal static class MiniInsurancePerQueryArtifacts
    {
        public const string FileName = "eval.perQuery.json";

        public static void TryPersist(string? runDir, IWorkflow workflow)
        {
            if (string.IsNullOrWhiteSpace(runDir))
                return;

            if (workflow is not IPerQueryEvalProvider provider)
                return;

            var path = Path.Combine(runDir, FileName);

            var json = JsonSerializer.Serialize(
                provider.PerQuery,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }
}
