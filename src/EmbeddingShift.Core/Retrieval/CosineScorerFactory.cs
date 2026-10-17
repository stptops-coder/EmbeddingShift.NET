using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Infrastructure;
using EmbeddingShift.Core.Utils;

namespace EmbeddingShift.Core.Retrieval;

public static class CosineScorerFactory
{
    public const string BackendVariable = "EMBEDDINGSHIFT_COSINE_BACKEND";
    public const string PythonVariable = "EMBEDDINGSHIFT_PYTHON_EXE";

    public static string BackendFromEnvironment()
    {
        var backend = (Environment.GetEnvironmentVariable(BackendVariable) ?? "csharp")
            .Trim().ToLowerInvariant();
        if (backend is not ("csharp" or "python"))
            throw new ArgumentException($"{BackendVariable} must be 'csharp' or 'python'.");
        return backend;
    }

    public static ICosineScorer FromEnvironment(Func<float[], float[], double>? csharpReference = null)
    {
        if (BackendFromEnvironment() == "csharp")
            return new CSharpCosineScorer(csharpReference ?? ((query, document) => VectorOps.Cosine(query, document)));

        var executable = Environment.GetEnvironmentVariable(PythonVariable);
        if (string.IsNullOrWhiteSpace(executable))
        {
            executable = "python";
            if (RepositoryLayout.TryResolveRepoRoot(out var root))
            {
                var local = Path.Combine(root, "python", ".venv",
                    OperatingSystem.IsWindows() ? "Scripts/python.exe" : "bin/python");
                if (File.Exists(local)) executable = local;
            }
        }
        var applicationDirectory = Path.GetDirectoryName(typeof(CosineScorerFactory).Assembly.Location);
        if (string.IsNullOrEmpty(applicationDirectory)) applicationDirectory = AppContext.BaseDirectory;
        return new PythonCosineScorer(executable,
            Path.Combine(applicationDirectory, "python", "cosine_worker.py"));
    }
}
