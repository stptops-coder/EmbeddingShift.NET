using System.Threading.Tasks;
namespace EmbeddingShift.ConsoleEval
{
    internal static class Workflow
    {
        public static async Task IngestAsync(string samplesPath, string dataset, string targetSlot)
            => await Task.CompletedTask;

        public static async Task EvalAsync(string dataset)
            => await Task.CompletedTask;
    }
}
