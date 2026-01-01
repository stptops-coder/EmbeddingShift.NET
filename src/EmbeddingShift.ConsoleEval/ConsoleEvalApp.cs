using System.Threading.Tasks;

namespace EmbeddingShift.ConsoleEval;

internal static class ConsoleEvalApp
{
    public static Task<int> RunAsync(string[] args)
    {
        var parsed = ConsoleEvalGlobalOptionsParser.Parse(args);

        // Apply env overrides (sim/cache/backend) BEFORE composition.
        ConsoleEvalGlobalEnvironment.Apply(parsed.Options);

        // Host is the UI-friendly facade; CLI remains an adapter.
        var host = ConsoleEvalHost.Create(parsed.Options);

        // Important: pass only command args to dispatcher (global flags removed).
        return ConsoleEvalCli.RunAsync(parsed.CommandArgs, host);
    }
}
