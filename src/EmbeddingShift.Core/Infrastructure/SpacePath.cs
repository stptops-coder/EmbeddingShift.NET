using System;
using System.IO;
using System.Linq;

namespace EmbeddingShift.Core.Infrastructure
{
    /// <summary>
    /// Canonical mapping from logical "space" (e.g. "DemoDataset:refs") to a filesystem-safe relative path.
    /// Keep this logic consistent across FileStore, ingest and eval.
    /// </summary>
    public static class SpacePath
    {
        public static string ToRelativePath(string space)
        {
            if (string.IsNullOrWhiteSpace(space))
                return "default";

            var parts = space
                .Split(new[] { ':', '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(SanitizePathPart)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            return parts.Length == 0 ? "default" : Path.Combine(parts);
        }

        private static string SanitizePathPart(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
                if (invalid.Contains(chars[i]))
                    chars[i] = '_';

            var sanitized = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "default" : sanitized;
        }
    }
}
