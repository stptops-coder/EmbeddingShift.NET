using System;
using System.IO;

namespace EmbeddingShift.Core.Infrastructure
{
    /// <summary>
    /// Resolves the repository root (folder containing both "src" and "samples").
    /// This is a convenience for CLI demo commands referencing sample data.
    /// </summary>
    public static class RepositoryLayout
    {
        private const string RepoRootEnvVar = "EMBEDDINGSHIFT_REPO_ROOT";

        public static bool TryResolveRepoRoot(out string repoRoot)
        {
            repoRoot = string.Empty;

            var fromEnv = Environment.GetEnvironmentVariable(RepoRootEnvVar);
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                var full = Path.GetFullPath(fromEnv);
                if (IsRepoRoot(full))
                {
                    repoRoot = full;
                    return true;
                }
            }

            var candidates = new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory,
                Path.GetFullPath(Path.Combine(DirectoryLayout.ResolveDataRoot(), ".."))
            };

            foreach (var start in candidates)
            {
                if (string.IsNullOrWhiteSpace(start))
                    continue;

                var dir = new DirectoryInfo(Path.GetFullPath(start));
                while (dir is not null)
                {
                    if (IsRepoRoot(dir.FullName))
                    {
                        repoRoot = dir.FullName;
                        return true;
                    }

                    dir = dir.Parent;
                }
            }

            return false;
        }

        public static string ResolveRepoRoot()
        {
            if (TryResolveRepoRoot(out var repoRoot))
                return repoRoot;

            throw new DirectoryNotFoundException(
                "Could not locate repository root (expected folders: 'src' and 'samples'). " +
                $"Set '{RepoRootEnvVar}' to the repo root directory.");
        }

        private static bool IsRepoRoot(string path)
        {
            if (!Directory.Exists(path))
                return false;

            return Directory.Exists(Path.Combine(path, "src"))
                && Directory.Exists(Path.Combine(path, "samples"));
        }
    }
}
