﻿using System.IO;

namespace EmbeddingShift.Workflows.Domains
{
    /// <summary>
    /// Describes the folder structure for the insurance domain.
    /// This keeps path conventions in a single place so tests,
    /// ingest and later evaluation can share the same layout.
    /// </summary>
    public static class InsuranceDomain
    {
        public const string RelativeRoot          = "samples/domains/insurance";
        public const string PoliciesSubfolder     = "policies";
        public const string ClaimsSubfolder       = "claims";
        public const string PreprocessedSubfolder = "preprocessed";

        public static string GetDomainRoot(string repoRoot) =>
            Path.Combine(repoRoot, "samples", "domains", "insurance");

        public static string GetPoliciesPath(string repoRoot) =>
            Path.Combine(GetDomainRoot(repoRoot), PoliciesSubfolder);

        public static string GetClaimsPath(string repoRoot) =>
            Path.Combine(GetDomainRoot(repoRoot), ClaimsSubfolder);

        public static string GetPreprocessedPath(string repoRoot) =>
            Path.Combine(GetDomainRoot(repoRoot), PreprocessedSubfolder);
    }
}
