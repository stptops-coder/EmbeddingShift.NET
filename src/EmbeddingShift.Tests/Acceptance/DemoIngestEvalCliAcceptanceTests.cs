using EmbeddingShift.ConsoleEval;
using EmbeddingShift.Core.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

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
                    ["EMBEDDINGSHIFT_DATA_ROOT"] = Path.Combine(tempRoot, "data"),
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
                var resultsDir = ExtractResultsDir(run.StdOut);

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
                Assert.True(File.Exists(refsManifestPath!), $"Refs manifest not found: {refsManifestPath}");

                var manifestsDir = Path.Combine(tempRoot, "data", "manifests", dataset, "refs");
                var refsManifestFull = Path.GetFullPath(refsManifestPath!);
                var manifestsDirFull = Path.GetFullPath(manifestsDir);

                Assert.StartsWith(manifestsDirFull, refsManifestFull, StringComparison.OrdinalIgnoreCase);

                // Robust: allow either manifest_latest.json OR manifest_<hash>.json
                var manifestFile = Path.GetFileName(refsManifestFull);
                var ok =
                    manifestFile.Equals("manifest_latest.json", StringComparison.OrdinalIgnoreCase) ||
                    (manifestFile.StartsWith("manifest_", StringComparison.OrdinalIgnoreCase) &&
                     manifestFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

                Assert.True(
                    ok,
                    $"Expected RefsManifestPath to reference a manifest json (manifest_latest.json or manifest_*.json), got: {refsManifestPath}"
                );

                var qDir = Path.Combine(tempRoot, "data", "embeddings", dataset, "queries");
                var rDir = Path.Combine(tempRoot, "data", "embeddings", dataset, "refs");

                Assert.True(Directory.Exists(qDir), $"Missing queries dir: {qDir}");
                Assert.True(Directory.Exists(rDir), $"Missing refs dir: {rDir}");

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

                // Baseline eval must persist a machine-readable acceptance gate manifest.
                var evalResultsDir2 = ExtractResultsDir(evalBaseline.StdOut);
                var evalGatePath = Path.Combine(evalResultsDir2, "acceptance_gate.json");
                Assert.True(File.Exists(evalGatePath), $"Missing acceptance gate manifest: {evalGatePath}");

                var evalGate = await ReadAcceptanceGateAsync(evalGatePath);
                Assert.True(evalGate.Passed, "Expected acceptance gate to pass for eval --baseline.");
                Assert.Equal("rank", evalGate.GateProfile);

                // Negative probe: a pathological shift must be caught by a stricter gate profile.
                var r2 = await RunDotnetAsync(env, consoleEvalDll,
                    "eval", dataset, "--baseline", "--shift=zero", "--gate-profile=rank+cosine");

                Assert.Equal(2, r2.ExitCode);
                Assert.Contains("Acceptance gate: FAIL", r2.StdOut);

                // Failing gates must still persist the manifest.
                var r2ResultsDir2 = ExtractResultsDir(r2.StdOut);
                var r2GatePath = Path.Combine(r2ResultsDir2, "acceptance_gate.json");
                Assert.True(File.Exists(r2GatePath), $"Missing acceptance gate manifest: {r2GatePath}");

                var r2Gate = await ReadAcceptanceGateAsync(r2GatePath);
                Assert.False(r2Gate.Passed, "Expected acceptance gate to fail for shift=zero with rank+cosine.");
                Assert.Equal("rank+cosine", r2Gate.GateProfile);

                // Same idea end-to-end: run (ingest+eval) should also fail with the same profile.
                var r3 = await RunDotnetAsync(env, consoleEvalDll,
                    "run", rPath, qPath, dataset, "--baseline", "--shift=zero", "--gate-profile=rank+cosine");

                Assert.Equal(2, r3.ExitCode);
                Assert.Contains("Acceptance gate: FAIL", r3.StdOut);

                // Failing end-to-end run must also persist the manifest.
                var r3ResultsDir2 = ExtractResultsDir(r3.StdOut);
                var r3GatePath = Path.Combine(r3ResultsDir2, "acceptance_gate.json");
                Assert.True(File.Exists(r3GatePath), $"Missing acceptance gate manifest: {r3GatePath}");

                var r3Gate = await ReadAcceptanceGateAsync(r3GatePath);
                Assert.False(r3Gate.Passed, "Expected acceptance gate to fail for run with shift=zero and rank+cosine.");
                Assert.Equal("rank+cosine", r3Gate.GateProfile);

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
                IsTruthy(Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_ACCEPTANCE_KEEP_ARTIFACTS"));

            try
            {
                var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["EMBEDDINGSHIFT_ROOT"] = tempRoot,
                    ["EMBEDDINGSHIFT_DATA_ROOT"] = Path.Combine(tempRoot, "data"),
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

                string? chunkFirstManifestPath = null;

                // Space-state must exist for refs and point to the concrete chunk-first manifest.
                var refsStatePath = Path.Combine(tempRoot, "data", "embeddings", dataset, "refs", "space_state_latest.json");
                Assert.True(File.Exists(refsStatePath), $"Missing refs space state: {refsStatePath}");

                var refsStateJson = await File.ReadAllTextAsync(refsStatePath);
                using (var stateDoc = JsonDocument.Parse(refsStateJson))
                {
                    if (stateDoc.RootElement.TryGetProperty("chunkFirstManifestPath", out var c1))
                        chunkFirstManifestPath = c1.GetString();
                    else if (stateDoc.RootElement.TryGetProperty("ChunkFirstManifestPath", out var c2))
                        chunkFirstManifestPath = c2.GetString();
                }

                Assert.False(string.IsNullOrWhiteSpace(chunkFirstManifestPath), "Refs space state must contain ChunkFirstManifestPath.");
                Assert.True(File.Exists(chunkFirstManifestPath!), $"Chunk-first manifest referenced by state not found: {chunkFirstManifestPath}");

                // Prove that eval does not rely only on manifest_latest.json.
                var manifestsDir = Path.Combine(tempRoot, "data", "manifests", dataset, "refs");
                var latestManifest = Path.Combine(manifestsDir, "manifest_latest.json");
                if (File.Exists(latestManifest))
                    File.Delete(latestManifest);

                // Now eval must load persisted embeddings.
                var evalBaseline = await RunDotnetAsync(env, consoleEvalDll, "eval", dataset, "--baseline");
                Assert.True(evalBaseline.ExitCode == 0, BuildFailureMessage("eval --baseline failed", tempRoot, evalBaseline));
                Assert.Contains("Eval mode: persisted embeddings", evalBaseline.StdOut);
                Assert.Contains("baseline=identity", evalBaseline.StdOut);

                // Lineage: eval should resolve refs manifest via the saved space-state.
                var resultsDir = ExtractResultsDir(evalBaseline.StdOut);

                var runManifestPath = Path.Combine(resultsDir, "run_manifest.json");
                Assert.True(File.Exists(runManifestPath), $"Missing run manifest: {runManifestPath}");

                var runJson = await File.ReadAllTextAsync(runManifestPath);
                using var runDoc = JsonDocument.Parse(runJson);

                string? refsManifestPath = null;

                if (runDoc.RootElement.TryGetProperty("refsManifestPath", out var p1))
                    refsManifestPath = p1.GetString();
                else if (runDoc.RootElement.TryGetProperty("RefsManifestPath", out var p2))
                    refsManifestPath = p2.GetString();

                Assert.False(
                    string.IsNullOrWhiteSpace(refsManifestPath),
                    "RefsManifestPath must be present in run_manifest.json even if manifest_latest.json was removed.");

                Assert.True(File.Exists(refsManifestPath!), $"Refs manifest not found: {refsManifestPath}");

                Assert.False(string.IsNullOrWhiteSpace(chunkFirstManifestPath));
                Assert.True(
                    string.Equals(
                        Path.GetFullPath(chunkFirstManifestPath!),
                        Path.GetFullPath(refsManifestPath!),
                        StringComparison.OrdinalIgnoreCase),
                    $"Expected eval to use state-linked manifest. State={chunkFirstManifestPath} vs RunManifest={refsManifestPath}");

                // Baseline eval must persist a machine-readable acceptance gate manifest.
                var evalResultsDir2 = ExtractResultsDir(evalBaseline.StdOut);
                var evalGatePath = Path.Combine(evalResultsDir2, "acceptance_gate.json");
                Assert.True(File.Exists(evalGatePath), $"Missing acceptance gate manifest: {evalGatePath}");

                var evalGate = await ReadAcceptanceGateAsync(evalGatePath);
                Assert.True(evalGate.Passed, "Expected acceptance gate to pass for eval --baseline.");
                Assert.Equal("rank", evalGate.GateProfile);
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
        public async Task SmokeDemo_RunSmokeDemo_PassesEndToEnd()
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
                    ["EMBEDDINGSHIFT_DATA_ROOT"] = Path.Combine(tempRoot, "data"),
                    ["EMBEDDING_BACKEND"] = "sim",
                    ["EMBEDDING_SIM_MODE"] = "deterministic",
                };

                var dataset = "DemoDataset";

                // End-to-end: reset -> ingest demo assets -> validate -> eval baseline -> gate PASS
                var smoke = await RunDotnetAsync(env, consoleEvalDll,
                    "run-smoke-demo", dataset, "--force-reset", "--baseline");

                Assert.True(smoke.ExitCode == 0, BuildFailureMessage("run-smoke-demo failed", tempRoot, smoke));

                // Robust key markers
                Assert.Contains("[SMOKE] PASS", smoke.StdOut);
                Assert.Contains("Validation: PASS", smoke.StdOut);
                Assert.Contains("Acceptance gate: PASS", smoke.StdOut);

                // Minimal artifact checks under temp root
                var refsEmbDir = Path.Combine(tempRoot, "data", "embeddings", dataset, "refs");
                var qEmbDir = Path.Combine(tempRoot, "data", "embeddings", dataset, "queries");

                Assert.True(Directory.Exists(refsEmbDir), $"Missing refs embeddings dir: {refsEmbDir}");
                Assert.True(Directory.Exists(qEmbDir), $"Missing queries embeddings dir: {qEmbDir}");

                var refsFiles = Directory.GetFiles(refsEmbDir, "*.json", SearchOption.TopDirectoryOnly);
                var qFiles = Directory.GetFiles(qEmbDir, "*.json", SearchOption.TopDirectoryOnly);

                Assert.True(refsFiles.Length >= 1, $"Expected >= 1 refs embedding file, found {refsFiles.Length}");
                Assert.True(qFiles.Length >= 1, $"Expected >= 1 query embedding file, found {qFiles.Length}");
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

            // Ensure acceptance tests are not influenced by the caller's shell environment.
            // The CLI may set these internally via global flags (e.g. --tenant), but the parent process must not leak them.
            psi.Environment.Remove("EMBEDDINGSHIFT_TENANT");
            psi.Environment.Remove("EMBEDDINGSHIFT_RESULTS_ROOT");
            psi.Environment.Remove("EMBEDDINGSHIFT_RESULTS_DOMAIN");

            foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;

            // Make demo commands deterministic: they must find samples/* regardless of EMBEDDINGSHIFT_ROOT (temp).
            var repoRoot = RepositoryLayout.ResolveRepoRoot();
            psi.WorkingDirectory = repoRoot;

            if (!psi.Environment.ContainsKey("EMBEDDINGSHIFT_REPO_ROOT"))
                psi.Environment["EMBEDDINGSHIFT_REPO_ROOT"] = repoRoot;

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

        private static async Task<AcceptanceGateInfo> ReadAcceptanceGateAsync(string gatePath)
        {
            var json = await File.ReadAllTextAsync(gatePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var passed = root.TryGetProperty("Passed", out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? p.GetBoolean()
                : false;

            var profile = root.TryGetProperty("GateProfile", out var gp)
                ? gp.GetString()
                : null;

            return new AcceptanceGateInfo(passed, profile);
        }

        private sealed record AcceptanceGateInfo(bool Passed, string? GateProfile);
        private sealed record ProcessRunResult(int ExitCode, string StdOut, string StdErr);
    }
}
