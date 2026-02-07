using EmbeddingShift.ConsoleEval;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EmbeddingShift.Tests.Acceptance
{
    /// <summary>
    /// Acceptance-style, black-box test that executes the ConsoleEval CLI
    /// (domain mini-insurance run --no-learned) in a child process and then
    /// validates that the expected artifacts were persisted under:
    ///   {EMBEDDINGSHIFT_ROOT}/results/insurance/(runs|aggregates)
    ///
    /// This protects:
    /// - CLI wiring (arg parsing + domain pack dispatch)
    /// - end-to-end workflow execution
    /// - file-system persistence layout (runs + aggregates)
    /// </summary>
    public sealed class MiniInsuranceFirstDeltaCliAcceptanceTests
    {
        [Fact]
        public async Task DomainMiniInsurance_Run_NoLearned_creates_runs_and_aggregate_metrics()
        {
            var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
            Assert.True(Directory.Exists(repoRoot), $"Repo root not found. BaseDirectory={AppContext.BaseDirectory}");

            // We reference ConsoleEval from the test project, so its assembly is available at runtime.
            var consoleEvalDll = typeof(EmbeddingBackend).Assembly.Location;
            Assert.True(File.Exists(consoleEvalDll), $"ConsoleEval assembly not found: {consoleEvalDll}");

            var tempRoot = Path.Combine(
                Path.GetTempPath(),
                "EmbeddingShift.Acceptance",
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(tempRoot);
            Console.WriteLine($"Acceptance TempRoot: {tempRoot}");

            var keepArtifacts =
                Debugger.IsAttached ||
                IsTruthy(Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_ACCEPTANCE_KEEP_ARTIFACTS"));

            try
            {
                var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["EMBEDDINGSHIFT_ROOT"] = tempRoot,
                    ["EMBEDDING_BACKEND"] = "sim",
                    ["EMBEDDING_SIM_MODE"] = "deterministic",
                };

                var args = new[]
                {
                    consoleEvalDll,
                    "domain",
                    "mini-insurance",
                    "run",
                    "--no-learned"
                };

                var result = await RunDotnetAsync(
                    workingDirectory: repoRoot,
                    arguments: args,
                    environment: env,
                    timeout: TimeSpan.FromMinutes(8));

                Assert.True(
                    result.ExitCode == 0,
                    BuildFailureMessage(
                        $"ConsoleEval exited with code {result.ExitCode}.",
                        tempRoot,
                        result));

                var resultsRoot = Path.Combine(tempRoot, "results", "insurance");
                var runsRoot = Path.Combine(resultsRoot, "runs");
                var aggregatesRoot = Path.Combine(resultsRoot, "aggregates");

                Assert.True(
                    Directory.Exists(runsRoot),
                    $"Runs root not found: {runsRoot}{Environment.NewLine}TempRoot={tempRoot}");

                var runDirs = Directory.GetDirectories(runsRoot, "*", SearchOption.TopDirectoryOnly);
                Assert.True(
                    runDirs.Length >= 3,
                    $"Expected >= 3 persisted run directories under {runsRoot} but found {runDirs.Length}.{Environment.NewLine}TempRoot={tempRoot}");

                Assert.True(
                    Directory.Exists(aggregatesRoot),
                    $"Aggregates root not found: {aggregatesRoot}{Environment.NewLine}TempRoot={tempRoot}");

                var aggregateDirs = Directory.GetDirectories(
                    aggregatesRoot,
                    "mini-insurance-first-delta-aggregate_*",
                    SearchOption.TopDirectoryOnly);

                Assert.NotEmpty(aggregateDirs);

                var latestAggregateDir = aggregateDirs.OrderBy(d => d).Last();

                Assert.True(
                    File.Exists(Path.Combine(latestAggregateDir, "metrics-aggregate.json")),
                    $"Missing metrics-aggregate.json under {latestAggregateDir}{Environment.NewLine}TempRoot={tempRoot}");

                Assert.True(File.Exists(Path.Combine(latestAggregateDir, "metrics-aggregate.md")),
                    $"Missing metrics-aggregate.md under {latestAggregateDir}{Environment.NewLine}TempRoot={tempRoot}");

                // --- Result validation (Golden Master for deterministic sim) ---
                var aggregateJsonPath = Path.Combine(latestAggregateDir, "metrics-aggregate.json");
                using var doc = JsonDocument.Parse(File.ReadAllText(aggregateJsonPath, Encoding.UTF8));
                var root = doc.RootElement;

                Assert.True(
                    root.TryGetProperty("ComparisonCount", out var cmpEl) && cmpEl.GetInt32() >= 1,
                    "Aggregate should include at least one comparison run.");

                var metricsEl = root.GetProperty("Metrics");

                var rows = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in metricsEl.EnumerateArray())
                {
                    var name = row.GetProperty("Metric").GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        rows[name!] = row;
                }

                Assert.True(rows.ContainsKey("map@1"), "Aggregate metrics must contain map@1.");
                Assert.True(rows.ContainsKey("ndcg@3"), "Aggregate metrics must contain ndcg@3.");

                static double Get(JsonElement row, string prop) => row.GetProperty(prop).GetDouble();

                static void AssertApprox(string name, double actual, double expected, double tol)
                {
                    Assert.True(
                        Math.Abs(actual - expected) <= tol,
                        $"{name}: expected {expected} (+/- {tol}) but was {actual}.");
                }

                const double Tol = 1e-12;

                var map = rows["map@1"];
                AssertApprox("map@1 AverageBaseline", Get(map, "AverageBaseline"), 0.9, Tol);
                AssertApprox("map@1 AverageFirst", Get(map, "AverageFirst"), 1.0, Tol);
                AssertApprox("map@1 AverageFirstPlusDelta", Get(map, "AverageFirstPlusDelta"), 1.0, Tol);

                var ndcg = rows["ndcg@3"];
                AssertApprox("ndcg@3 AverageBaseline", Get(ndcg, "AverageBaseline"), 0.9261859507142916, Tol);
                AssertApprox("ndcg@3 AverageFirst", Get(ndcg, "AverageFirst"), 1.0, Tol);
                AssertApprox("ndcg@3 AverageFirstPlusDelta", Get(ndcg, "AverageFirstPlusDelta"), 1.0, Tol);
            }
            finally
            {
                if (!keepArtifacts)
                {
                    try { Directory.Delete(tempRoot, recursive: true); }
                    catch { /* keep best-effort silent */ }
                }
            }
        }

        private static bool IsTruthy(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var v = value.Trim().ToLowerInvariant();
            return v is "1" or "true" or "yes" or "y" or "on";
        }

        private static string FindRepoRoot(string startDirectory)
        {
            var dir = new DirectoryInfo(startDirectory);

            for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "EmbeddingShift.sln");
                if (File.Exists(candidate))
                    return dir.FullName;
            }

            // Fallback to current directory to make the error message informative.
            return Directory.GetCurrentDirectory();
        }

        private sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr);

        private static async Task<ProcessRunResult> RunDotnetAsync(
            string workingDirectory,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string> environment,
            TimeSpan timeout)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in arguments)
                psi.ArgumentList.Add(arg);

            // Ensure acceptance tests are not influenced by the caller's shell environment.
            // The CLI may set these internally via global flags (e.g. --tenant), but the parent process must not leak them.
            psi.Environment.Remove("EMBEDDINGSHIFT_TENANT");
            psi.Environment.Remove("EMBEDDINGSHIFT_RESULTS_ROOT");
            psi.Environment.Remove("EMBEDDINGSHIFT_RESULTS_DOMAIN");

            foreach (var kvp in environment)
                psi.Environment[kvp.Key] = kvp.Value;

            using var process = new Process { StartInfo = psi };

            var started = process.Start();
            if (!started)
                return new ProcessRunResult(ExitCode: -1, StdOut: string.Empty, StdErr: "Failed to start process.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeout);

            try
            {
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return new ProcessRunResult(ExitCode: -2, StdOut: await stdoutTask.ConfigureAwait(false), StdErr: "Timed out.");
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            return new ProcessRunResult(process.ExitCode, stdout, stderr);
        }

        private static string BuildFailureMessage(string headline, string tempRoot, ProcessRunResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine(headline);
            sb.AppendLine();
            sb.AppendLine($"TempRoot: {tempRoot}");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(result.StdOut))
            {
                sb.AppendLine("=== STDOUT ===");
                sb.AppendLine(result.StdOut.Trim());
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(result.StdErr))
            {
                sb.AppendLine("=== STDERR ===");
                sb.AppendLine(result.StdErr.Trim());
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
