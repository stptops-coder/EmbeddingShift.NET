using EmbeddingShift.Agentic;

namespace EmbeddingShift.Agentic.Console;

public static class AgenticConsoleGlobalEnvironment
{
    public static void Apply(AgenticConsoleGlobalOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        Environment.SetEnvironmentVariable(
            AgenticReasoningClientFactory.BackendEnvironmentVariable,
            options.ReasoningBackend);
    }
}
