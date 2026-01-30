using EmbeddingShift.ConsoleEval;
using EmbeddingShift.Core.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace EmbeddingShift.Tests.Acceptance
{
    /// <summary>
    /// Regression guard for the unified tenant results layout.
    ///
    /// Goal:
    /// - Any tenant-scoped command must write under: results/{domainKey}/tenants/{tenant}/...
    ///   with domainKey defaulting to "insurance" unless overridden.
    /// - Runs commands must resolve their default runs root under the same tenant layout
    ///   (without requiring --runs-root).
    ///
    /// This test uses EMBEDDINGSHIFT_ROOT to isolate artifacts in a temp folder.
    /// </summary>
    public sealed class ResultsRootLayoutAcceptanceTests
    {
        [Fact]
        public async Task Run_WithTenant_WritesUnderInsuranceTenantRoot()
        {
            var consoleEvalDll = typeof(EmbeddingBackend).Assembly.Location;
            Assert.True(File.Exists(consoleEvalDll), $"ConsoleEval assembly not found: {consoleEvalDll}");

            var tempRoot = CreateTempRoot();
            var keepArtifacts =
                Debugger.IsAttached ||
                IsTruthy(Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_ACCEPTANCE_KEEP_ARTIFACTS"));

            try
            {
                var tenant = "insurer-a";
                var dataset = "LayoutGateDataset";

                var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["EMBEDDINGSHIFT_ROOT"] = tempRoot,
                    ["EMBEDDING_BACKEND"] = "sim",
                    ["EMBEDDING_SIM_MODE"] = "deterministic",
                };

                Directory.CreateDirectory(tempRoot);

                var qPath = Path.Combine(tempRoot, "q.txt");
                var rPath = Path.Combine(tempRoot, "r.txt");

                await File.WriteAllTextAsync(qPath, "query one\nquery two\nquery three\n");
                await File.WriteAllTextAsync(rPath, "answer one\nanswer two\nanswer three\n");

                var run = await RunDotnetAsync(env, consoleEvalDll, "--tenant", tenant, "run", rPath, qPath, dataset);
                Assert.True(run.ExitCode == 0, BuildFailureMessage("run failed", tempRoot, run));

                var resultsDir = ExtractResultsDir(run.StdOut);

                var expectedPrefix = Path.GetFullPath(Path.Combine(tempRoot, "results", "insurance", "tenants", tenant));
                var actualFull = Path.GetFullPath(resultsDir);

                Assert.True(actualFull.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase),
                $"Expected results dir to start with: {expectedPrefix}{Environment.NewLine}Actual: {actualFull}");
            }
            finally
            {
                if (!keepArtifacts)
                    TryDeleteDirectory(tempRoot);
            }
        }

        [Fact]
        public async Task RunsCompare_DefaultRoot_UsesInsuranceTenantRunsFolder()
        {
            var consoleEvalDll = typeof(EmbeddingBackend).Assembly.Location;
            Assert.True(File.Exists(consoleEvalDll), $"ConsoleEval assembly not found: {consoleEvalDll}");

            var tempRoot = CreateTempRoot();
            var keepArtifacts =
                Debugger.IsAttached ||
                IsTruthy(Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_ACCEPTANCE_KEEP_ARTIFACTS"));

            try
            {
                var tenant = "insurer-a";

                var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["EMBEDDINGSHIFT_ROOT"] = tempRoot,
                    ["EMBEDDING_BACKEND"] = "sim",
                    ["EMBEDDING_SIM_MODE"] = "deterministic",
                };

                Directory.CreateDirectory(tempRoot);

                // Intentionally do NOT create any runs folder. The command may either:
                // - return non-zero and print a "runs root not found" hint, OR
                // - return zero but still report the resolved root and that no runs are available.
                var r = await RunDotnetAsync(env, consoleEvalDll, "--tenant", tenant, "runs-compare", "--metric", "ndcg@3");

                var expectedRunsRoot = Path.GetFullPath(Path.Combine(tempRoot, "results", "insurance", "tenants", tenant, "runs"));
                var stdout = (r.StdOut ?? string.Empty) + Environment.NewLine + (r.StdErr ?? string.Empty);

                Assert.True(stdout.IndexOf(expectedRunsRoot, StringComparison.OrdinalIgnoreCase) >= 0,
                    $"Expected runs-compare output to mention the unified runs root.{Environment.NewLine}" +
                    $"Expected: {expectedRunsRoot}{Environment.NewLine}{stdout}");

                var indicatesMissingOrEmpty =
                    stdout.IndexOf("Runs root not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    stdout.IndexOf("runs   = 0", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    stdout.IndexOf("runs = 0", StringComparison.OrdinalIgnoreCase) >= 0;

                Assert.True(indicatesMissingOrEmpty,
                    "Expected runs-compare to indicate an empty/missing runs root (without crashing)." +
                    Environment.NewLine + stdout);

            }
            finally
            {
                if (!keepArtifacts)
                    TryDeleteDirectory(tempRoot);
            }
        }

        private static string CreateTempRoot()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "EmbeddingShift.Acceptance",
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
                Guid.NewGuid().ToString("N"));
        }

        private static bool IsTruthy(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractResultsDir(string stdout)
        {
            var marker = "| Results at ";
            var mi = stdout.LastIndexOf(marker, StringComparison.Ordinal);
            Assert.True(mi >= 0, "Missing results path marker in stdout.");

            return stdout
                .Substring(mi + marker.Length)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0]
                .Trim();
        }

        private static string BuildFailureMessage(string title, string tempRoot, ProcessRunResult run)
        {
            var sb = new StringBuilder();
            sb.AppendLine(title);
            sb.AppendLine($"TempRoot: {tempRoot}");
            sb.AppendLine($"ExitCode: {run.ExitCode}");
            sb.AppendLine("--- STDOUT ---");
            sb.AppendLine(run.StdOut);
            sb.AppendLine("--- STDERR ---");
            sb.AppendLine(run.StdErr);
            return sb.ToString();
        }

        private static async Task<ProcessRunResult> RunDotnetAsync(
            IDictionary<string, string> env,
            string consoleEvalDll,
            params string[] args)
        {
            var psi = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            psi.ArgumentList.Add(consoleEvalDll);
            foreach (var a in args) psi.ArgumentList.Add(a);

            foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;

            // Ensure demo/sample commands (if any) can still resolve repo-root assets.
            var repoRoot = RepositoryLayout.ResolveRepoRoot();
            psi.WorkingDirectory = repoRoot;

            if (!psi.Environment.ContainsKey("EMBEDDINGSHIFT_REPO_ROOT"))
                psi.Environment["EMBEDDINGSHIFT_REPO_ROOT"] = repoRoot;

            using var p = Process.Start(psi);
            if (p is null)
                throw new InvalidOperationException("Failed to start dotnet process for acceptance test.");

            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            var stderrTask = p.StandardError.ReadToEndAsync();

            await Task.WhenAll(stdoutTask, stderrTask);
            await p.WaitForExitAsync();

            return new ProcessRunResult(
                p.ExitCode,
                stdoutTask.Result ?? string.Empty,
                stderrTask.Result ?? string.Empty);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        private sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr);
    }
}
