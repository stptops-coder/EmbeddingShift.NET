using System;
using System.Threading.Tasks;

namespace EmbeddingShift.Agentic.Console;

public static class AgenticConsoleApp
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var parsed = AgenticConsoleGlobalOptionsParser.Parse(args);
            AgenticConsoleGlobalEnvironment.Apply(parsed.Options);

            var host = AgenticConsoleHost.Create(parsed.Options);
            return await AgenticConsoleCli.RunAsync(parsed.CommandArgs, host).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"ERROR: {ex.Message}");
            Environment.ExitCode = 1;
            return 1;
        }
    }
}
