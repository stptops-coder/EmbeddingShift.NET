using EmbeddingShift.ConsoleEval.Domains;

namespace EmbeddingShift.ConsoleEval.Commands;

/// <summary>
/// Domain-pack CLI commands ("domain" + legacy aliases).
/// Extracted from Program.cs to keep the composition root small.
/// </summary>
public static class DomainCliCommands
{
    public static async Task<int> DomainAsync(string[] args)
    {
        // Entry point for domain packs (towards multi-domain ConsoleEval).
        //
        // Usage:
        //   domain list
        //   domain <domainId> <subcommand> [...]

        var sub = args.Length >= 2 ? args[1] : "list";

        if (IsHelp(sub))
        {
            PrintDomainEntryHelp();
            return 0;
        }


        if (string.Equals(sub, "list", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sub, "--list", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Available domain packs:");
            foreach (var pack in DomainPackRegistry.All)
            {
                Console.WriteLine($"  {pack.DomainId,-18} {pack.DisplayName}");
            }
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance pipeline");
            return 0;
        }

        var packById = DomainPackRegistry.TryGet(sub);
        if (packById is null)
        {
            Console.WriteLine($"Unknown domain pack '{sub}'.");
            Console.WriteLine();
            Console.WriteLine("Use:");
            Console.WriteLine("  domain list");
            return 0;
        }

        var subArgs = args.Skip(2).ToArray();
        var exitCode = await packById.ExecuteAsync(subArgs, msg => Console.WriteLine(msg));
        if (exitCode != 0)
        {
            Environment.ExitCode = exitCode;
        }

        return exitCode;
    }

    public static async Task<int> ExecuteDomainPackAsync(string domainId, string[] subArgs)
    {
        var pack = DomainPackRegistry.TryGet(domainId);
        if (pack is null)
        {
            Console.WriteLine($"Unknown domain pack '{domainId}'.");
            return 1;
        }

        var exitCode = await pack.ExecuteAsync(subArgs, msg => Console.WriteLine(msg));
        if (exitCode != 0)
        {
            Environment.ExitCode = exitCode;
        }

        return exitCode;
    }


    private static bool IsHelp(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        return token.Equals("help", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
               token.Equals("-h", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintDomainEntryHelp()
    {
        Console.WriteLine("Domain packs — usage");
        Console.WriteLine("  domain list");
        Console.WriteLine("  domain <domainId> <subcommand> [...]");
        Console.WriteLine();
        Console.WriteLine("Tip:");
        Console.WriteLine("  domain <domainId> help");
        Console.WriteLine("  domain <domainId> --help");
        Console.WriteLine();
        Console.WriteLine("Available domain packs:");
        foreach (var pack in DomainPackRegistry.All)
        {
            Console.WriteLine($"  {pack.DomainId,-18} {pack.DisplayName}");
        }
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance pipeline");
    }
}
