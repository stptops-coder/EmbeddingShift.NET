using EmbeddingShift.Abstractions;

namespace EmbeddingShift.ConsoleEval;

public sealed class ConsoleRunLogger : IRunLogger
{
    public Guid StartRun(string kind, string dataset)
    {
        var id = Guid.NewGuid();
        Console.WriteLine($"[RUN START] {id} | {kind} | {dataset}");
        return id;
    }

    public void LogMetric(Guid runId, string metric, double score)
        => Console.WriteLine($"[RUN {runId}] {metric} = {score:F4}");

    public void CompleteRun(Guid runId, string resultsPath)
        => Console.WriteLine($"[RUN END] {runId} | Results at {resultsPath}");
}
