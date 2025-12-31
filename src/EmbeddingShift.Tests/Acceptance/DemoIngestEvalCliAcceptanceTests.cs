using EmbeddingShift.ConsoleEval;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
 
namespace EmbeddingShift.Tests.Acceptance
{
    /// <summary>
    /// Acceptance gate for the demo ingest commands:
    /// ingest-queries + ingest-refs (persisted) -> eval (loads persisted embeddings).
    ///
    /// Goal: keep the ingest stream stable and reproducible as we evolve the canonical
    /// data layout and chunk-first ingest pipeline.
    /// </summary>
    public sealed class DemoIngestEvalCliAcceptanceTests
    {
        [Fact]
        public async Task DemoIngestThenEval_LoadsPersistedEmbeddings()
        {
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

                var dataset = "DemoDataset";

                var qPath = Path.Combine(tempRoot, "q.txt");
                var rPath = Path.Combine(tempRoot, "r.txt");

                await File.WriteAllTextAsync(qPath, "query one\nquery two\nquery three\n");
                await File.WriteAllTextAsync(rPath, "answer one\nanswer two\nanswer three\n");

                var run = await RunDotnetAsync(env, consoleEvalDll, "run", rPath, qPath, dataset);
                Assert.True(run.ExitCode == 0, BuildFailureMessage("run failed", tempRoot, run));

                Assert.Contains("Eval mode: persisted embeddings", run.StdOut);
                // Lineage: ensure eval persisted a run manifest into the result directory.
                var marker = "| Results at ";
                var mi = run.StdOut.LastIndexOf(marker, StringComparison.Ordinal);
                Assert.True(mi >= 0, "Missing results path marker in stdout.");

                var resultsDir = run.StdOut
                    .Substring(mi + marker.Length)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0]
                    .Trim();

                var runManifestPath = Path.Combine(resultsDir, "run_manifest.json");
                Assert.True(File.Exists(runManifestPath), $"Missing run manifest: {runManifestPath}");

                var json = await File.ReadAllTextAsync(runManifestPath);
                using var doc = JsonDocument.Parse(json);

                string? refsManifestPath = null;

                if (doc.RootElement.TryGetProperty("refsManifestPath", out var p1))
                    refsManifestPath = p1.GetString();
                else if (doc.RootElement.TryGetProperty("RefsManifestPath", out var p2))
                    refsManifestPath = p2.GetString();


                Assert.False(string.IsNullOrWhiteSpace(refsManifestPath), "RefsManifestPath must be present in run_manifest.json.");
                Assert.True(
                    refsManifestPath!.EndsWith("manifest_latest.json", StringComparison.OrdinalIgnoreCase),
                    $"Expected RefsManifestPath to end with manifest_latest.json, got: {refsManifestPath}"
                );

                var qDir = Path.Combine(tempRoot, "data", "embeddings", dataset, "queries");
                var rDir = Path.Combine(tempRoot, "data", "embeddings", dataset, "refs");

                Assert.True(Directory.Exists(qDir), $"Missing queries dir: {qDir}");
                Assert.True(Directory.Exists(rDir), $"Missing refs dir: {rDir}");

                var manifestsDir = Path.Combine(tempRoot, "data", "manifests", dataset, "refs");
                Assert.True(Directory.Exists(manifestsDir), $"Missing manifests dir: {manifestsDir}");

                var manifestFiles = Directory.GetFiles(manifestsDir, "manifest_*.json", SearchOption.TopDirectoryOnly);
                Assert.True(manifestFiles.Length >= 1, $"Expected >= 1 refs manifest, found {manifestFiles.Length}");

                var latestManifest = Path.Combine(manifestsDir, "manifest_latest.json");
                Assert.True(File.Exists(latestManifest), $"Missing latest refs manifest: {latestManifest}");

                var qFiles = Directory.GetFiles(qDir, "*.json", SearchOption.TopDirectoryOnly);
                var rFiles = Directory.GetFiles(rDir, "*.json", SearchOption.TopDirectoryOnly);

                Assert.True(qFiles.Length >= 3, $"Expected >= 3 query embeddings, found {qFiles.Length}");
                Assert.True(rFiles.Length >= 1, $"Expected >= 1 ref embedding (chunk-first), found {rFiles.Length}");

                // Baseline compare mode should run on persisted embeddings and emit baseline/shift/delta metrics.
                var evalBaseline = await RunDotnetAsync(env, consoleEvalDll, "eval", dataset, "--baseline");
                Assert.True(evalBaseline.ExitCode == 0, BuildFailureMessage("eval --baseline failed", tempRoot, evalBaseline));

                Assert.Contains("evaluation+baseline", evalBaseline.StdOut);
                Assert.Contains("CosineSimilarityEvaluator.baseline", evalBaseline.StdOut);
                Assert.Contains("CosineSimilarityEvaluator.shift", evalBaseline.StdOut);
                Assert.Contains("CosineSimilarityEvaluator.delta", evalBaseline.StdOut);
                Assert.Contains("baseline=identity", evalBaseline.StdOut);

                // Negative probe: a pathological shift must be caught by a stricter gate profile.
                var r2 = await RunDotnetAsync(env, consoleEvalDll,
                    "eval", dataset, "--baseline", "--shift=zero", "--gate-profile=rank+cosine");

                Assert.Equal(2, r2.ExitCode);
                Assert.Contains("Acceptance gate: FAIL", r2.StdOut);

                // Same idea end-to-end: run (ingest+eval) should also fail with the same profile.
                var r3 = await RunDotnetAsync(env, consoleEvalDll,
                    "run", rPath, qPath, dataset, "--baseline", "--shift=zero", "--gate-profile=rank+cosine");

                Assert.Equal(2, r3.ExitCode);
                Assert.Contains("Acceptance gate: FAIL", r3.StdOut);

                Assert.Contains("Acceptance gate: PASS", evalBaseline.StdOut);

            }
            finally
            {
                if (!keepArtifacts)
                {
                    try { Directory.Delete(tempRoot, recursive: true); }
                    catch { /* best-effort */ }
                }
            }
        }

        [Fact]
        public async Task DemoIngestDatasetThenEval_LoadsPersistedEmbeddings()
        {
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
                string.Equals(Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_KEEP_ACCEPTANCE_ARTIFACTS"), "1", StringComparison.OrdinalIgnoreCase);

            try
            {
                var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["EMBEDDINGSHIFT_ROOT"] = tempRoot,
                    ["EMBEDDING_BACKEND"] = "sim",
                    ["EMBEDDING_SIM_MODE"] = "deterministic",
                };

                var dataset = "DemoDataset";

                var qPath = Path.Combine(tempRoot, "q.txt");
                var rPath = Path.Combine(tempRoot, "r.txt");

                await File.WriteAllTextAsync(qPath, "query one\nquery two\nquery three\n");
                await File.WriteAllTextAsync(rPath, "answer one\nanswer two\nanswer three\n");

                var ingest = await RunDotnetAsync(env, consoleEvalDll, "ingest-dataset", rPath, qPath, dataset);
                Assert.True(ingest.ExitCode == 0, BuildFailureMessage("ingest-dataset failed", tempRoot, ingest));
                Assert.Contains("Ingest (dataset) finished.", ingest.StdOut);

                // Now eval must load persisted embeddings.
                var evalBaseline = await RunDotnetAsync(env, consoleEvalDll, "eval", dataset, "--baseline");
                Assert.True(evalBaseline.ExitCode == 0, BuildFailureMessage("eval --baseline failed", tempRoot, evalBaseline));
                Assert.Contains("Eval mode: persisted embeddings", evalBaseline.StdOut);
                Assert.Contains("baseline=identity", evalBaseline.StdOut);
            }
            finally
            {
                if (!keepArtifacts)
                {
                    try { Directory.Delete(tempRoot, recursive: true); }
                    catch { /* best-effort */ }
                }
            }
        }

        private static bool IsTruthy(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
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

            using var p = Process.Start(psi);
            if (p is null) throw new InvalidOperationException("Failed to start dotnet process.");

            var stdout = await p.StandardOutput.ReadToEndAsync();
            var stderr = await p.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            await p.WaitForExitAsync(cts.Token);

            return new ProcessRunResult(p.ExitCode, stdout, stderr);
        }

        private static string BuildFailureMessage(string headline, string tempRoot, ProcessRunResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine(headline);
            sb.AppendLine($"TempRoot={tempRoot}");
            sb.AppendLine($"ExitCode={result.ExitCode}");
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

        private sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr);
    }
}
