using System;
using System.Collections.Generic;

namespace EmbeddingShift.Agentic.Console;

public static class AgenticConsoleGlobalOptionsParser
{
    public static AgenticConsoleParsedArgs Parse(string[] args)
    {
        args ??= Array.Empty<string>();

        var commandArgs = new List<string>();
        var backend = "sim";

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.Equals("--backend", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException("Missing value for --backend.");
                }

                backend = args[++i];
                continue;
            }

            const string backendPrefix = "--backend=";
            if (arg.StartsWith(backendPrefix, StringComparison.OrdinalIgnoreCase))
            {
                backend = arg[backendPrefix.Length..];
                continue;
            }

            commandArgs.Add(arg);
        }

        if (string.IsNullOrWhiteSpace(backend))
        {
            backend = "sim";
        }

        return new AgenticConsoleParsedArgs(
            new AgenticConsoleGlobalOptions(backend.Trim()),
            commandArgs.ToArray());
    }
}
