using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EmbeddingShift.Abstractions;
using EmbeddingShift.ConsoleEval;
using EmbeddingShift.Core.Infrastructure;
using Xunit;

namespace EmbeddingShift.Tests
{
    /// <summary>
    /// Verifies that the MiniInsurancePosNegRunner can run end-to-end against
    /// the simulated embedding backend and creates a metrics-posneg.json/md
    /// pair in the results directory.
    ///
    /// This is an integration-style test that protects the basic contract of
    /// the pos-neg evaluation command.
    /// </summary>
    public class MiniInsurancePosNegRunnerTests
    {
        [Fact]
        public async Task RunAsync_with_sim_backend_creates_metrics_files()
        {
            var tempRoot = CreateTestTempRoot("mini-insurance-posneg");
            using var _ = new TestEnvScope(
                new Dictionary<string, string?>
                {
                    ["EMBEDDINGSHIFT_ROOT"] = tempRoot,
                    ["EMBEDDINGSHIFT_DATA_ROOT"] = Path.Combine(tempRoot, "data"),
                },
                keepArtifacts: Debugger.IsAttached || IsTruthy(Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_TEST_KEEP_ARTIFACTS")));

            // Arrange: resolve the same results root that the runner uses (but redirected to temp).
            var root = DirectoryLayout.ResolveResultsRoot("insurance");
            var runsRoot = Path.Combine(root, "runs");
            Directory.CreateDirectory(runsRoot);

            var pattern = "mini-insurance-posneg-run_*";
            var before = Directory.GetDirectories(runsRoot, pattern, SearchOption.TopDirectoryOnly);

            // Act
            await MiniInsurancePosNegRunner.RunAsync(EmbeddingBackend.Sim);

            // Assert: there should be at least one (new) run directory
            var after = Directory.GetDirectories(runsRoot, pattern, SearchOption.TopDirectoryOnly);
            Assert.NotEmpty(after);

            var newDirs = after.Except(before).ToArray();
            var targetDir = (newDirs.Length > 0 ? newDirs : after)
                .OrderBy(d => d)
                .Last();

            Assert.True(File.Exists(Path.Combine(targetDir, "metrics-posneg.json")));
            Assert.True(File.Exists(Path.Combine(targetDir, "metrics-posneg.md")));
        }

        private static string CreateTestTempRoot(string testName)
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var root = Path.Combine(Path.GetTempPath(), "EmbeddingShift.Tests", testName, stamp, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Console.WriteLine($"Test TempRoot: {root}");
            return root;
        }

        private static bool IsTruthy(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var v = value.Trim().ToLowerInvariant();
            return v is "1" or "true" or "yes" or "y";
        }

        private sealed class TestEnvScope : IDisposable
        {
            private readonly Dictionary<string, string?> _previous;
            private readonly string? _tempRoot;
            private readonly bool _keepArtifacts;

            public TestEnvScope(Dictionary<string, string?> vars, bool keepArtifacts)
            {
                _keepArtifacts = keepArtifacts;
                _previous = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in vars)
                {
                    _previous[kvp.Key] = Environment.GetEnvironmentVariable(kvp.Key);
                    Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
                }

                _tempRoot = vars.TryGetValue("EMBEDDINGSHIFT_ROOT", out var r) ? r : null;
            }

            public void Dispose()
            {
                foreach (var kvp in _previous)
                    Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);

                if (_keepArtifacts) return;
                if (string.IsNullOrWhiteSpace(_tempRoot)) return;

                try
                {
                    Directory.Delete(_tempRoot, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup only. Never fail the test on filesystem cleanup.
                }
            }
        }
    }
}
