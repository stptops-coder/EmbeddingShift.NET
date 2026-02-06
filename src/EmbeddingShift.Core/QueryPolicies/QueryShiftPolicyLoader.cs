using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EmbeddingShift.Core.QueryPolicies
{
    /// <summary>
    /// Loads and normalizes QueryShiftPolicy from JSON.
    /// </summary>
    public static class QueryShiftPolicyLoader
    {
        private static readonly JsonSerializerOptions _json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static QueryShiftPolicy Load(string policyPath)
        {
            if (string.IsNullOrWhiteSpace(policyPath))
                throw new ArgumentException("Policy path must not be empty.", nameof(policyPath));

            if (!File.Exists(policyPath))
                throw new FileNotFoundException("Query shift policy file not found.", policyPath);

            var json = File.ReadAllText(policyPath);
            var policy = JsonSerializer.Deserialize<QueryShiftPolicy>(json, _json);

            if (policy is null)
                throw new InvalidOperationException($"Failed to deserialize QueryShiftPolicy: {policyPath}");

            // Normalize mapping with case-insensitive keys and normalized absolute file paths.
            var baseDir = Path.GetDirectoryName(Path.GetFullPath(policyPath)) ?? Environment.CurrentDirectory;

            var normalized = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in policy.QueryIdToShiftTrainingResults ?? new Dictionary<string, List<string>>())
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                    continue;

                var list = kvp.Value ?? new List<string>();

                var paths = list
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => NormalizePath(baseDir, p!))
                    .ToList();

                // Validate referenced artifacts early so failures are loud + deterministic.
                foreach (var p in paths)
                {
                    if (!File.Exists(p))
                        throw new FileNotFoundException($"ShiftTrainingResult file referenced by policy does not exist: {p}", p);
                }

                normalized[kvp.Key.Trim()] = paths;
            }

            return new QueryShiftPolicy
            {
                Version = policy.Version,
                Name = policy.Name,
                QueryIdToShiftTrainingResults = normalized
            };
        }

        private static string NormalizePath(string baseDir, string path)
        {
            var trimmed = path.Trim();

            if (Path.IsPathRooted(trimmed))
                return Path.GetFullPath(trimmed);

            return Path.GetFullPath(Path.Combine(baseDir, trimmed));
        }
    }
}
