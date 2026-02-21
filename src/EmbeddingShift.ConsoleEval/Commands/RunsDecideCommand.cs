﻿using EmbeddingShift.Core.Runs;
using EmbeddingShift.Core.Infrastructure;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EmbeddingShift.ConsoleEval.Commands
{
    public static class RunsDecideCommand
    {
        public static Task RunAsync(string[] args)
        {
            // Usage:
            //   runs-decide [--runs-root=<path>] [--domainKey=<key>] [--metric=<key>] [--profile=<key>] [--eps=<double>] [--write] [--apply] [--include-repo-posneg] [--open]
            //
            // Defaults:
            //   domainKey = insurance
            //   tenant    = ENV:EMBEDDINGSHIFT_TENANT (or "insurer-a")
            //   runs-root = .\\results\\<domainKey>\\tenants\\<tenant>\\runs
            //   metric    = ndcg@3
            //   eps       = 1e-6
            //   write     = true
            //   apply     = false (dry decision only)
            //
            // Notes:
            // - 'apply' uses RunActivation.Promote(...) and will create/overwrite the active pointer for this metric.
            // - This command does not delete any run directories.

            var runsRoot = GetOpt(args, "--runs-root");
            var domainKey = GetOpt(args, "--domainKey") ?? "insurance";
            var metricKey = GetOpt(args, "--metric") ?? "ndcg@3";
            var profileKey = GetOpt(args, "--profile");
            if (string.IsNullOrWhiteSpace(profileKey)) profileKey = null;
            var epsText = GetOpt(args, "--eps");
            var write = HasSwitch(args, "--write") || !HasSwitch(args, "--no-write");
            var apply = HasSwitch(args, "--apply");
            var open = HasSwitch(args, "--open");
            var includeRepoPosNeg = HasSwitch(args, "--include-repo-posneg");

            var eps = 1e-6;
            if (!string.IsNullOrWhiteSpace(epsText) &&
                !double.TryParse(epsText, NumberStyles.Float, CultureInfo.InvariantCulture, out eps))
            {
                Console.WriteLine($"[runs-decide] Invalid --eps value: {epsText}");
                Environment.ExitCode = 1;
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(runsRoot))
            {
                var tenant = Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_TENANT");
                if (string.IsNullOrWhiteSpace(tenant)) tenant = "insurer-a";
                Environment.SetEnvironmentVariable("EMBEDDINGSHIFT_TENANT", tenant);

                runsRoot = Path.Combine(DirectoryLayout.ResolveResultsRoot(domainKey), "runs");
            }

            if (!Directory.Exists(runsRoot))
            {
                Console.WriteLine($"[runs-decide] Runs root not found: {runsRoot}");
                Environment.ExitCode = 1;
                return Task.CompletedTask;
            }

            RunPromotionDecision decision;
            try
            {
                decision = RunPromotionDecider.Decide(runsRoot, metricKey, profileKey, eps, includeRepoPosNeg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[runs-decide] Failed: {ex.Message}");
                Environment.ExitCode = 1;
                return Task.CompletedTask;
            }

            PrintConsole(decision, profileKey);

            string? decisionDir = null;
            string? mdPath = null;
            string? jsonPath = null;

            if (write)
            {
                var outDir = Path.Combine(runsRoot, "_decisions");
                Directory.CreateDirectory(outDir);

                var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
                var safeMetric = SanitizeFileName(metricKey);

                mdPath = Path.Combine(outDir, $"decision_{safeMetric}_{stamp}.md");
                jsonPath = Path.Combine(outDir, $"decision_{safeMetric}_{stamp}.json");

                File.WriteAllText(mdPath, RenderMarkdown(decision, profileKey), new UTF8Encoding(false));

                var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    WriteIndented = true,
                    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
                };

                var json = JsonSerializer.Serialize(decision, jsonOptions);
                File.WriteAllText(jsonPath, json, new UTF8Encoding(false));

                decisionDir = outDir;

                Console.WriteLine();
                Console.WriteLine($"[runs-decide] Wrote: {mdPath}");
                Console.WriteLine($"[runs-decide] Wrote: {jsonPath}");
            }

            if (apply && decision.Action == RunPromotionDecisionAction.Promote)
            {
                try
                {
                    var result = RunActivation.PromoteExplicit(
                        runsRoot,
                        metricKey,
                        profileKey,
                        decision.Candidate.WorkflowName,
                        decision.Candidate.RunId,
                        decision.Candidate.Score,
                        decision.Candidate.RunDirectory,
                        decision.Candidate.RunJsonPath,
                        decision.TotalRunsFound);

                    Console.WriteLine();
                    Console.WriteLine($"[runs-decide] PROMOTED → Active directory: {result.Pointer.RunDirectory}");
                    Console.WriteLine($"[runs-decide] Active pointer: {result.ActivePath}");

                    if (!string.IsNullOrWhiteSpace(result.PreviousActiveArchivedTo))
                        Console.WriteLine($"[runs-decide] Archived previous active to: {result.PreviousActiveArchivedTo}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[runs-decide] Promotion failed: {ex.Message}");
                    Environment.ExitCode = 1;
                    return Task.CompletedTask;
                }
            }
            else if (apply)
            {
                Console.WriteLine();
                Console.WriteLine("[runs-decide] No promotion performed (decision != PROMOTE).");
            }

            if (open)
            {
                try
                {
                    var target = decisionDir ?? runsRoot;
                    var psi = new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true };
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

        private static void PrintConsole(RunPromotionDecision d, string? profileKey)
        {
            Console.WriteLine($"[runs-decide] root   = {d.RunsRoot}");
            Console.WriteLine($"[runs-decide] metric = {d.MetricKey}");
            if (!string.IsNullOrWhiteSpace(profileKey))
                Console.WriteLine($"[runs-decide] profile= {profileKey}");
            Console.WriteLine($"[runs-decide] eps    = {d.Epsilon.ToString("0.######", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"[runs-decide] runs   = {d.TotalRunsFound}");
            Console.WriteLine();

            Console.WriteLine($"Candidate: {d.Candidate.WorkflowName}");
            Console.WriteLine($"  runId     : {d.Candidate.RunId}");
            Console.WriteLine($"  score     : {d.Candidate.Score:0.000000}");
            Console.WriteLine($"  dir       : {d.Candidate.RunDirectory}");
            Console.WriteLine();

            if (d.Active is null)
            {
                Console.WriteLine("Active   : <none>");
            }
            else
            {
                Console.WriteLine($"Active   : {d.Active.WorkflowName}");
                Console.WriteLine($"  runId     : {d.Active.RunId}");
                Console.WriteLine($"  score     : {d.Active.Score:0.000000}");
                Console.WriteLine($"  dir       : {d.Active.RunDirectory}");
            }

            Console.WriteLine();
            Console.WriteLine($"Decision : {d.Action}");
            Console.WriteLine($"Delta    : {FormatDelta(d.Delta)}");
            Console.WriteLine($"Reason   : {d.Reason}");
        }

        private static string RenderMarkdown(RunPromotionDecision d, string? profileKey)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# runs-decide");
            sb.AppendLine();
            sb.AppendLine($"- utc: `{d.CreatedUtc:O}`");
            sb.AppendLine($"- runsRoot: `{d.RunsRoot}`");
            if (!string.IsNullOrWhiteSpace(profileKey)) sb.AppendLine($"- profile: `{profileKey}`");
            sb.AppendLine($"- metric: `{d.MetricKey}`");
            sb.AppendLine($"- eps: `{d.Epsilon.ToString("0.######", CultureInfo.InvariantCulture)}`");
            sb.AppendLine($"- totalRunsFound: `{d.TotalRunsFound}`");
            sb.AppendLine();
            sb.AppendLine("## Candidate");
            sb.AppendLine();
            sb.AppendLine($"- workflow: `{d.Candidate.WorkflowName}`");
            sb.AppendLine($"- runId: `{d.Candidate.RunId}`");
            sb.AppendLine($"- score: `{d.Candidate.Score:0.000000}`");
            sb.AppendLine($"- dir: `{d.Candidate.RunDirectory}`");
            sb.AppendLine($"- run.json: `{d.Candidate.RunJsonPath}`");
            sb.AppendLine();
            sb.AppendLine("## Active");
            sb.AppendLine();

            if (d.Active is null)
            {
                sb.AppendLine("- <none>");
            }
            else
            {
                sb.AppendLine($"- workflow: `{d.Active.WorkflowName}`");
                sb.AppendLine($"- runId: `{d.Active.RunId}`");
                sb.AppendLine($"- score: `{d.Active.Score:0.000000}`");
                sb.AppendLine($"- dir: `{d.Active.RunDirectory}`");
                sb.AppendLine($"- run.json: `{d.Active.RunJsonPath}`");
            }

            sb.AppendLine();
            sb.AppendLine("## Decision");
            sb.AppendLine();
            sb.AppendLine($"- action: `{d.Action}`");
            sb.AppendLine($"- delta: `{FormatDelta(d.Delta)}`");
            sb.AppendLine($"- reason: {d.Reason}");

            return sb.ToString();
        }

        private static string FormatDelta(double delta)
        {
            if (double.IsPositiveInfinity(delta)) return "+inf";
            if (double.IsNegativeInfinity(delta)) return "-inf";
            if (double.IsNaN(delta)) return "NaN";
            return delta.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string? GetOpt(string[] args, string key)
        {
            // Supports both "--name=value" and "--name value".
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

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);

            foreach (var ch in name)
                sb.Append(invalid.Contains(ch) ? '_' : ch);

            return sb.ToString();
        }
    }
}
