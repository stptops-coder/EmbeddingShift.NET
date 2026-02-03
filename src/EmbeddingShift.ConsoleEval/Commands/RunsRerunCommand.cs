using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EmbeddingShift.Core.Runs;

namespace EmbeddingShift.ConsoleEval.Commands;

/// <summary>
/// Replays the command that produced a run, based on run_request.json.
/// This is intentionally minimal and primarily targets deterministic simulation runs.
/// </summary>
public static class RunsRerunCommand
{
    public static async Task RunAsync(string[] args)
    {
        // Usage:
        //   runs-rerun --run-dir=<path> [--print] [--keep-env]
        //
        // Notes:
        // - Reads <run-dir>/run_request.json and replays the captured CLI args.
        // - By default, applies the captured environment snapshot (EMBEDDING_* vars).
        //   Use --keep-env to preserve the current process environment.

        var runDir = GetOpt(args, "--run-dir") ?? GetOpt(args, "--runDir");
        var print = HasSwitch(args, "--print");
        var keepEnv = HasSwitch(args, "--keep-env");

        if (string.IsNullOrWhiteSpace(runDir))
        {
            Console.WriteLine("[runs-rerun] Missing required option: --run-dir=<path>");
            Environment.ExitCode = 2;
            return;
        }

        if (!Directory.Exists(runDir))
        {
            Console.WriteLine($"[runs-rerun] Run directory not found: {runDir}");
            Environment.ExitCode = 1;
            return;
        }

        var reqPath = Path.Combine(runDir, "run_request.json");
        if (!File.Exists(reqPath))
        {
            Console.WriteLine($"[runs-rerun] run_request.json not found: {reqPath}");
            Console.WriteLine("Hint: run_request.json is written only when a RunRequestContext is present during persistence.");
            Environment.ExitCode = 3;
            return;
        }

        var json = await File.ReadAllTextAsync(reqPath, Encoding.UTF8).ConfigureAwait(false);
        var artifact = JsonSerializer.Deserialize<WorkflowRunRequestArtifact>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        if (artifact is null)
        {
            Console.WriteLine("[runs-rerun] Failed to parse run_request.json (artifact is null).");
            Environment.ExitCode = 3;
            return;
        }

        if (!keepEnv && artifact.Request.EnvironmentSnapshot is not null)
        {
            foreach (var kv in artifact.Request.EnvironmentSnapshot)
            {
                if (!string.IsNullOrWhiteSpace(kv.Key))
                    Environment.SetEnvironmentVariable(kv.Key, kv.Value);
            }
        }

        var replayArgs = artifact.Request.GlobalArgs
            .Concat(artifact.Request.CommandArgs)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToArray();

        Console.WriteLine("[runs-rerun] Replaying:");
        Console.WriteLine("  " + string.Join(' ', replayArgs));
        Console.WriteLine();

        if (print)
            return;

        var exitCode = await EmbeddingShift.ConsoleEval.ConsoleEvalApp.RunAsync(replayArgs).ConfigureAwait(false);
        if (exitCode != 0)
            Environment.ExitCode = exitCode;
    }

    private static string? GetOpt(string[] args, string key)
    {
        if (args is null || args.Length == 0) return null;

        var match = args.FirstOrDefault(a =>
            a.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(match)) return null;

        var parts = match.Split('=', 2);
        return parts.Length == 2 ? parts[1].Trim() : null;
    }

    private static bool HasSwitch(string[] args, string key)
    {
        if (args is null || args.Length == 0) return false;
        return args.Any(a => string.Equals(a, key, StringComparison.OrdinalIgnoreCase));
    }
}
