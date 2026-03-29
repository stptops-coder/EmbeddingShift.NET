using System.IO;
using EmbeddingShift.Core.Infrastructure;

namespace EmbeddingShift.Tests
{
    internal static class TestPathHelper
    {
        public static string GetRepositoryRoot()
        {
            return RepositoryLayout.ResolveRepoRoot();
        }

        public static string GetInsuranceDomainDirectory()
        {
            var repoRoot = GetRepositoryRoot();

            var candidates = new[]
            {
                Path.Combine(repoRoot, "samples", "domains", "insurance"),
                Path.Combine(repoRoot, "samples", "insurance"),
                Path.Combine(repoRoot, "data", "domains", "insurance"),
                Path.Combine(repoRoot, "data", "insurance")
            };

            foreach (var candidate in candidates)
            {
                if (Directory.Exists(candidate))
                    return candidate;
            }

            return candidates[0];
        }
    }
}
