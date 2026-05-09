using EmbeddingShift.Agentic.EmbeddingShift;

namespace EmbeddingShift.Agentic.Console;

public static class AgenticConsoleComposition
{
    public static AgenticConsoleServices CreateServices(AgenticConsoleGlobalOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var workflow = SimulatedAgenticRetrievalWorkflow.CreateFromEnvironment();
        return new AgenticConsoleServices(workflow);
    }
}
