using EmbeddingShift.Agentic.Console;

namespace EmbeddingShift.Agentic.Tests;

public class AgenticConsoleCliTests
{
    [Fact]
    public async Task Agentic_console_run_sim_returns_success()
    {
        var exitCode = await AgenticConsoleApp.RunAsync(new[] { "--backend", "sim", "run-sim" });

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Agentic_console_keeps_openai_backend_deferred()
    {
        var exitCode = await AgenticConsoleApp.RunAsync(new[] { "--backend", "openai", "run-sim" });

        Assert.Equal(1, exitCode);
    }
}
