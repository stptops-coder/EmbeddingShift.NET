using System;
using System.IO;

namespace EmbeddingShift.Core.Infrastructure
{
    /// <summary>
    /// Central helper for resolving the physical layout of data and results folders.
    /// Keeps runtime code independent from Debug/Release and current working directory.
    /// This is intentionally conservative and falls back to the current directory
    /// if all primary candidates fail.
    /// </summary>
    public static class DirectoryLayout
    {
        public static string ResolveResultsRoot(string? domainSubfolder = null)
        {
            // Tenant-aware result layout (unified):
            // - No tenant: results[/<domainSubfolder>]
            // - With tenant: results/<domainKey>/tenants/<tenant>[/...]
            //
            // Historically, some commands wrote to results/tenants/<tenant>/..., while others used
            // results/<domainKey>/tenants/<tenant>/... (e.g. "insurance"). This created confusing
            // cross-command inconsistencies. We now always route tenant results through the
            // domainKey layout, using a stable default domainKey when none is provided.
            var tenant = GetTenantKeyOrNull();
            if (tenant is null)
                return ResolveRoot("results", domainSubfolder);

            var domainKey = domainSubfolder ?? GetDefaultResultsDomainKeyOrNull() ?? "insurance";
            domainKey = SanitizeFolderKey(domainKey);

            var tenantPart = Path.Combine("tenants", tenant);
            var effective = Path.Combine(domainKey, tenantPart);

            return ResolveRoot("results", effective);
        }

        private static string? GetDefaultResultsDomainKeyOrNull()
        {
            // Optional override (keeps the layout flexible for future domain packs).
            // Example: set EMBEDDINGSHIFT_RESULTS_DOMAIN=pharma to route tenant results under results/pharma/tenants/<tenant>/...
            var raw = Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_RESULTS_DOMAIN");
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            return raw;
        }

private static string? GetTenantKeyOrNull()
        {
            var raw = Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_TENANT");
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            return SanitizeFolderKey(raw);
        }

        private static string SanitizeFolderKey(string value)
        {
            // Predictable + filesystem-safe:
            // - lower invariant
            // - allow [a-z0-9-_]
            // - everything else -> '-'
            // - trim leading/trailing '-'
            var s = value.Trim().ToLowerInvariant();
            if (s.Length == 0)
                return "tenant";

            var chars = s.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                var ok =
                    (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') ||
                    c == '-' ||
                    c == '_';
                if (!ok)
                    chars[i] = '-';
            }

            var cleaned = new string(chars).Trim('-');
            return cleaned.Length == 0 ? "tenant" : cleaned;
        }


        public static string ResolveDataRoot(string? domainSubfolder = null)
            => ResolveRoot("data", domainSubfolder);

        private static string ResolveRoot(string rootFolderName, string? domainSubfolder)
        {
            if (string.IsNullOrWhiteSpace(rootFolderName))
                throw new ArgumentException("Root folder name must be provided.", nameof(rootFolderName));

            string Combine(params string[] parts) => Path.Combine(parts);

            var envRoot = Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_ROOT");

            var candidates = new[]
            {
                // Optional override (lets CI or users pin a stable location)
                string.IsNullOrWhiteSpace(envRoot)
                    ? null
                    : Path.GetFullPath(Combine(envRoot, rootFolderName)),

                // repo-root/results or /data (typical dev layout)
                Path.GetFullPath(Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", rootFolderName)),

                // current working directory/results or /data (fallback)
                Combine(Directory.GetCurrentDirectory(), rootFolderName),

                // bin/Debug/.../results or /data (last resort only)
                Combine(AppContext.BaseDirectory, rootFolderName),
            };

            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                try
                {
                    var path = domainSubfolder is null
                        ? candidate
                        : Combine(candidate, domainSubfolder);

                    Directory.CreateDirectory(path);
                    return path;
                }
                catch
                {
                    // try next candidate, keep this silent and robust
                }
            }


            // Last-resort fallback: current directory (+ optional subfolder)
            var fallback = domainSubfolder is null
                ? Directory.GetCurrentDirectory()
                : Combine(Directory.GetCurrentDirectory(), domainSubfolder);

            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }
}
