using System;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Agentic.Console;

public sealed class AgenticConsoleHost
{
    public AgenticConsoleGlobalOptions Options { get; }
    public AgenticConsoleServices Services { get; }

    private AgenticConsoleHost(AgenticConsoleGlobalOptions options, AgenticConsoleServices services)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public static AgenticConsoleHost Create(AgenticConsoleGlobalOptions options)
    {
        var services = AgenticConsoleComposition.CreateServices(options);
        return new AgenticConsoleHost(options, services);
    }

    public Task<WorkflowResult> RunSimulationAsync(CancellationToken ct = default)
    {
        var runner = new StatsAwareWorkflowRunner();
        return runner.ExecuteAsync("Agentic-Sim", Services.SimulationWorkflow, ct);
    }
}
