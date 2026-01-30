using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmbeddingShift.ConsoleEval;
using EmbeddingShift.ConsoleEval.Domains;
using Xunit;

namespace EmbeddingShift.Tests;

public class ConsoleEvalCliDispatchTests
{
    private static readonly object ConsoleLock = new();

    [Fact]
    public async Task HelpSweep_AllCommandsAcceptHelpAndDoNotThrow()
    {
        var host = ConsoleEvalHost.Create(new ConsoleEvalGlobalOptions());

        var helpOutput = await InvokeAsync(host, new[] { "help" });
        var commands = ParseTopLevelCommands(helpOutput.Output);

        Assert.True(commands.Count > 0, "Expected to discover at least one CLI command from top-level help output.");

        foreach (var cmd in commands)
        {
            var r = await InvokeAsync(host, new[] { cmd, "--help" });
            Assert.True(r.ExitCode == 0, $"Command '{cmd} --help' returned {r.ExitCode}.{Environment.NewLine}{r.Output}");
        }

        // Domain entry help
        {
            var r = await InvokeAsync(host, new[] { "domain", "--help" });
            Assert.True(r.ExitCode == 0, $"Command 'domain --help' returned {r.ExitCode}.{Environment.NewLine}{r.Output}");
        }

        // Domain pack root help + nested help (unsupported, but must not be mis-parsed)
        foreach (var pack in DomainPackRegistry.All)
        {
            var id = pack.DomainId;

            var r1 = await InvokeAsync(host, new[] { "domain", id, "--help" });
            Assert.True(r1.ExitCode == 0, $"Command 'domain {id} --help' returned {r1.ExitCode}.{Environment.NewLine}{r1.Output}");

            var r2 = await InvokeAsync(host, new[] { "domain", id, "pipeline", "--help" });
            Assert.True(r2.ExitCode == 0, $"Command 'domain {id} pipeline --help' returned {r2.ExitCode}.{Environment.NewLine}{r2.Output}");
        }
    }

    private static IReadOnlyList<string> ParseTopLevelCommands(string helpOutput)
    {
        var result = new List<string>();

        using var sr = new StringReader(helpOutput);
        string? line;
        var inCommands = false;

        while ((line = sr.ReadLine()) != null)
        {
            if (!inCommands)
            {
                if (line.Trim().Equals("Commands:", StringComparison.OrdinalIgnoreCase))
                    inCommands = true;
                continue;
            }

            // Stop at the next section.
            if (line.TrimStart().StartsWith("Global flags", StringComparison.OrdinalIgnoreCase))
                break;

            // Command lines look like: "  <name>   <summary>"
            if (!line.StartsWith("  ", StringComparison.Ordinal)) continue;

            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            var name = trimmed.Split(' ', '	').FirstOrDefault();
            if (string.IsNullOrWhiteSpace(name)) continue;

            result.Add(name);
        }

        // De-dup
        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private sealed record InvokeResult(int ExitCode, string Output);

    private static Task<InvokeResult> InvokeAsync(ConsoleEvalHost host, string[] args)
    {
        lock (ConsoleLock)
        {
            var originalOut = Console.Out;
            var originalErr = Console.Error;

            try
            {
                var sb = new StringBuilder();
                using var sw = new StringWriter(sb);

                Console.SetOut(sw);
                Console.SetError(sw);

                return InvokeCoreAsync(host, args, sb, sw, originalOut, originalErr);
            }
            catch
            {
                Console.SetOut(originalOut);
                Console.SetError(originalErr);
                throw;
            }
        }
    }

    private static async Task<InvokeResult> InvokeCoreAsync(
        ConsoleEvalHost host,
        string[] args,
        StringBuilder sb,
        StringWriter sw,
        TextWriter originalOut,
        TextWriter originalErr)
    {
        try
        {
            var code = await ConsoleEvalCli.RunAsync(args, host);
            sw.Flush();
            return new InvokeResult(code, sb.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
}
