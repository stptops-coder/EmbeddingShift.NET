using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using EmbeddingShift.Workflows.Agentic;

namespace EmbeddingShift.Tests;

public class SimulatedAgenticRetrievalWorkflowTests
{
    [Fact]
    public async Task Agentic_simulation_calls_retrieval_adaptation_and_simulated_reasoning()
    {
        var workflow = new SimulatedAgenticRetrievalWorkflow();
        var runner = new StatsAwareWorkflowRunner();

        var artifacts = await runner.ExecuteAsync("Agentic-Sim", workflow);

        Assert.True(artifacts.Success);
        Assert.NotNull(artifacts.Metrics);
        Assert.Equal(1d, artifacts.Metrics!["agentic.retrievalAdaptation.calls"]);
        Assert.Equal(1d, artifacts.Metrics!["agentic.reasoning.calls"]);
        Assert.Equal(1d, artifacts.Metrics!["agentic.reasoning.simulated"]);
        Assert.Equal(0d, artifacts.Metrics!["agentic.llmCalls.actual"]);
        Assert.Equal(1d, artifacts.Metrics!["agentic.llmCalls.simulated"]);
        Assert.Equal(1d, artifacts.Metrics!["agentic.top1.changed"]);
        Assert.Equal(1d, artifacts.Metrics!["agentic.top1.corrected"]);
        Assert.Equal(1d, artifacts.Metrics!["agentic.answer.generated"]);
        Assert.Contains("No external LLM call was made", artifacts.Notes ?? string.Empty);
    }

    [Fact]
    public void Agentic_simulation_is_registered_as_toy_workflow()
    {
        var registry = RegistryDefaults.CreateWithToyWorkflows();

        var workflow = registry.Resolve("agentic-sim");

        Assert.Equal("Agentic-Ready-Retrieval-Simulation", workflow.Name);
    }

    [Fact]
    public void Agentic_reasoning_factory_keeps_openai_as_deferred_boundary()
    {
        var ex = Assert.Throws<NotSupportedException>(() => AgenticReasoningClientFactory.FromKey("openai"));

        Assert.Contains("not wired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
