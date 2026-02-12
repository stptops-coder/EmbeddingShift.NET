using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EmbeddingShift.Core.Infrastructure;
using EmbeddingShift.Core.Runs;

namespace EmbeddingShift.ConsoleEval.Commands
{
    public static class RunsPromoteCommand
    {
        public static Task RunAsync(string[] args)
        {
            // Usage:
            //   runs-promote [--runs-root=<path>] [--domainKey=<key>] [--metric=<key>] [--rank=<n>] [--runid=<id>] [--open]
            //
            // Defaults:
            //   domainKey = insurance
            //   tenant    = ENV:EMBEDDINGSHIFT_TENANT (or "insurer-a")
            //   runs-root = .\results\<domainKey>\tenants\<tenant>\runs
            //   metric    = ndcg@3

            var runsRoot = GetOpt(args, "--runs-root");
            var domainKey = GetOpt(args, "--domainKey") ?? "insurance";
            var metricKey = GetOpt(args, "--metric") ?? "ndcg@3";

            var rankText = GetOpt(args, "--rank");
            int? pickRank = null;
            if (!string.IsNullOrWhiteSpace(rankText) &&
                int.TryParse(rankText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedRank) &&
                parsedRank > 0)
            {
                pickRank = parsedRank;
            }

            var pickRunId = GetOpt(args, "--runid");
            if (string.IsNullOrWhiteSpace(pickRunId))
            {
                pickRunId = null;
            }

            var open = HasSwitch(args, "--open");

            if (string.IsNullOrWhiteSpace(runsRoot))
            {
                var tenant = Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_TENANT");
                if (string.IsNullOrWhiteSpace(tenant)) tenant = "insurer-a";
                Environment.SetEnvironmentVariable("EMBEDDINGSHIFT_TENANT", tenant);

                runsRoot = Path.Combine(DirectoryLayout.ResolveResultsRoot(domainKey), "runs");
            }

            if (!Directory.Exists(runsRoot))
            {
                Console.WriteLine($"[runs-promote] Runs root not found: {runsRoot}");
                Environment.ExitCode = 1;
                return Task.CompletedTask;
            }

            RunActivation.PromoteResult result;
            try
            {
                result = RunActivation.Promote(runsRoot, metricKey, pickRank, pickRunId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[runs-promote] Failed: {ex.Message}");
                Environment.ExitCode = 1;
                return Task.CompletedTask;
            }

            if (result.Pointer is null)
            {
                Console.WriteLine($"[runs-promote] root   = {runsRoot}");
                Console.WriteLine($"[runs-promote] metric = {metricKey}");
                if (pickRank is not null) Console.WriteLine($"[runs-promote] rank   = {pickRank}");
                if (pickRunId is not null) Console.WriteLine($"[runs-promote] runid  = {pickRunId}");
                Console.WriteLine();
                Console.WriteLine("No runs found that contain the requested metric (or selection did not match).");
                Environment.ExitCode = 2;
                return Task.CompletedTask;
            }

            Console.WriteLine($"[runs-promote] root   = {runsRoot}");
            Console.WriteLine($"[runs-promote] metric = {metricKey}");
            if (pickRank is not null) Console.WriteLine($"[runs-promote] rank   = {pickRank}");
            if (pickRunId is not null) Console.WriteLine($"[runs-promote] runid  = {pickRunId}");
            Console.WriteLine();
            Console.WriteLine($"Active directory: {result.Pointer.RunDirectory}");
            Console.WriteLine($"WorkflowName    : {result.Pointer.WorkflowName}");
            Console.WriteLine($"RunId           : {result.Pointer.RunId}");
            Console.WriteLine($"Score           : {result.Pointer.Score:0.000000}");
            Console.WriteLine();
            Console.WriteLine($"[runs-promote] Wrote: {result.ActivePath}");

            if (!string.IsNullOrWhiteSpace(result.PreviousActiveArchivedTo))
                Console.WriteLine($"[runs-promote] Archived previous active to: {result.PreviousActiveArchivedTo}");

            if (open)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo(result.Pointer.RunDirectory) { UseShellExecute = true };
                    System.Diagnostics.Process.Start(psi);
                }
                catch
                {
                    // ignore
                }
            }

            Environment.ExitCode = 0;
            return Task.CompletedTask;
        }

        private static string? GetOpt(string[] args, string key)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];

                if (a.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                    return a.Substring(key.Length + 1).Trim().Trim('"');

                if (string.Equals(a, key, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    return args[i + 1].Trim().Trim('"');
            }

            return null;
        }

        private static bool HasSwitch(string[] args, string key)
            => args.Any(a => string.Equals(a, key, StringComparison.OrdinalIgnoreCase));
    }
}
