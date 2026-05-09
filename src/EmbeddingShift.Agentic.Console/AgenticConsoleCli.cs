using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Agentic.Console;

public static class AgenticConsoleCli
{
    private sealed record CommandSpec(string Name, string Summary, Func<string[], Task<int>> Handler);

    public static async Task<int> RunAsync(string[] args, AgenticConsoleHost host)
    {
        if (host is null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        Environment.ExitCode = 0;
        var commands = BuildCommands(host);

        if (args.Length == 0)
        {
            PrintHelp(commands);
            return 0;
        }

        var cmd = args[0];
        if (IsHelp(cmd))
        {
            PrintHelp(commands);
            return 0;
        }

        if (cmd.Equals("--version", StringComparison.OrdinalIgnoreCase))
        {
            PrintVersion();
            return 0;
        }

        if (!commands.TryGetValue(cmd, out var spec))
        {
            System.Console.WriteLine($"Unknown command '{cmd}'.");
            System.Console.WriteLine();
            PrintHelp(commands);
            Environment.ExitCode = 1;
            return 1;
        }

        if (args.Length > 1 && args.Skip(1).Any(IsHelp))
        {
            System.Console.WriteLine($"Note: per-command help is not implemented for '{cmd}'.");
            System.Console.WriteLine("Use: dotnet run --project src/EmbeddingShift.Agentic.Console -- help");
            System.Console.WriteLine();
            PrintHelp(commands);
            return 0;
        }

        var exitCode = await spec.Handler(args).ConfigureAwait(false);
        if (exitCode != 0)
        {
            Environment.ExitCode = exitCode;
        }

        return exitCode;
    }

    private static IReadOnlyDictionary<string, CommandSpec> BuildCommands(AgenticConsoleHost host)
    {
        var map = new Dictionary<string, CommandSpec>(StringComparer.OrdinalIgnoreCase);

        void Add(string name, string summary, Func<string[], Task<int>> handler, params string[] aliases)
        {
            var spec = new CommandSpec(name, summary, handler);
            map[name] = spec;

            foreach (var alias in aliases.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                map[alias] = spec;
            }
        }

        Add("run-sim", "run the local agentic retrieval/reasoning simulation", _ => RunSimulationAsync(host), "sim");

        return map;
    }

    private static async Task<int> RunSimulationAsync(AgenticConsoleHost host)
    {
        var result = await host.RunSimulationAsync().ConfigureAwait(false);
        PrintWorkflowResult(result);
        return result.Success ? 0 : 1;
    }

    private static void PrintWorkflowResult(WorkflowResult result)
    {
        System.Console.WriteLine("[agentic] simulation finished");
        System.Console.WriteLine($"Success: {result.Success}");

        if (!string.IsNullOrWhiteSpace(result.Notes))
        {
            System.Console.WriteLine($"Notes: {result.Notes}");
        }

        if (result.Metrics is { Count: > 0 })
        {
            System.Console.WriteLine("Metrics:");
            foreach (var metric in result.Metrics.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                System.Console.WriteLine($"  {metric.Key} = {metric.Value.ToString("0.###", CultureInfo.InvariantCulture)}");
            }
        }
    }

    private static bool IsHelp(string value)
        => value.Equals("help", StringComparison.OrdinalIgnoreCase)
           || value.Equals("--help", StringComparison.OrdinalIgnoreCase)
           || value.Equals("-h", StringComparison.OrdinalIgnoreCase);

    private static void PrintHelp(IReadOnlyDictionary<string, CommandSpec> commands)
    {
        System.Console.WriteLine("EmbeddingShift.Agentic.Console");
        System.Console.WriteLine("Usage:");
        System.Console.WriteLine("  dotnet run --project src/EmbeddingShift.Agentic.Console -- [global options] <command>");
        System.Console.WriteLine();
        System.Console.WriteLine("Global options:");
        System.Console.WriteLine("  --backend <sim|openai>    Reasoning backend. Current supported: sim. Deferred: openai.");
        System.Console.WriteLine();
        System.Console.WriteLine("Commands:");

        foreach (var spec in commands.Values.DistinctBy(x => x.Name).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            System.Console.WriteLine($"  {spec.Name,-12} {spec.Summary}");
        }
    }

    private static void PrintVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var version = asm.GetName().Version?.ToString() ?? "unknown";
        System.Console.WriteLine($"EmbeddingShift.Agentic.Console {version}");
    }
}
