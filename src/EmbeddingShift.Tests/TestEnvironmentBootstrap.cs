using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace EmbeddingShift.Tests;

/// <summary>
/// Ensures tests do not write artifacts into the repository (./data, ./results).
/// We redirect the process-level roots to a temp directory at module load time.
/// </summary>
internal static class TestEnvironmentBootstrap
{
    private static string? _tempRoot;

    [ModuleInitializer]
    internal static void Init()
    {
        // Allow local overrides (useful when debugging).
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_ROOT")))
            return;

        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "EmbeddingShift.Tests",
            DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_tempRoot);

        var dataRoot = Path.Combine(_tempRoot, "data");
        Directory.CreateDirectory(dataRoot);

        Environment.SetEnvironmentVariable("EMBEDDINGSHIFT_ROOT", _tempRoot);
        Environment.SetEnvironmentVariable("EMBEDDINGSHIFT_DATA_ROOT", dataRoot);

        AppDomain.CurrentDomain.ProcessExit += (_, _) => TryCleanup();
    }

    private static void TryCleanup()
    {
        // Opt-out switch: keep artifacts for inspection.
        var keep = string.Equals(
            Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_TEST_KEEP_ARTIFACTS"),
            "1",
            StringComparison.OrdinalIgnoreCase);

        if (keep)
            return;

        if (_tempRoot is null)
            return;

        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}
