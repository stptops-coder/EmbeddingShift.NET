using System;
using System.IO;

namespace EmbeddingShift.Core.Infrastructure
{
    public static class DataRepositoryLayout
    {
        private const string DataRootEnvVar = "EMBEDDINGSHIFT_DATA_ROOT";

        public static string GetDataRoot()
        {
            var fromEnv = Environment.GetEnvironmentVariable(DataRootEnvVar);
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                var full = Path.GetFullPath(fromEnv);
                Directory.CreateDirectory(full);
                return full;
            }

            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrWhiteSpace(baseDir))
            {
                baseDir = Directory.GetCurrentDirectory();
            }

            var defaultRoot = Path.Combine(baseDir, "EmbeddingShiftData");
            Directory.CreateDirectory(defaultRoot);
            return defaultRoot;
        }

        public static string GetDomainRoot(string domainName)
        {
            if (string.IsNullOrWhiteSpace(domainName))
                throw new ArgumentException("Domain name must not be null or empty.", nameof(domainName));

            var path = Path.Combine(GetDataRoot(), "domains", domainName);
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetDocumentsRawRoot(string domainName)
        {
            var path = Path.Combine(GetDomainRoot(domainName), "documents", "raw");
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetDocumentsNormalizedRoot(string domainName)
        {
            var path = Path.Combine(GetDomainRoot(domainName), "documents", "normalized");
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetChunksRoot(string domainName)
        {
            var path = Path.Combine(GetDomainRoot(domainName), "documents", "chunks");
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetQueriesRoot(string domainName)
        {
            var path = Path.Combine(GetDomainRoot(domainName), "queries");
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetEmbeddingsRoot(string domainName, string corpusKind)
        {
            if (string.IsNullOrWhiteSpace(corpusKind))
                throw new ArgumentException("Corpus kind must not be null or empty.", nameof(corpusKind));

            var path = Path.Combine(GetDomainRoot(domainName), "embeddings", corpusKind);
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetRunsRoot(string domainName, string workflowName)
        {
            if (string.IsNullOrWhiteSpace(workflowName))
                throw new ArgumentException("Workflow name must not be null or empty.", nameof(workflowName));

            var path = Path.Combine(GetDomainRoot(domainName), "runs", workflowName);
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetComparisonsRoot(string domainName, string workflowName)
        {
            var path = Path.Combine(GetRunsRoot(domainName, workflowName), "comparisons");
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetTrainingRunsRoot(string domainName, string workflowName)
        {
            var path = Path.Combine(GetRunsRoot(domainName, workflowName), "training");
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetEvaluationRunsRoot(string domainName, string workflowName)
        {
            var path = Path.Combine(GetRunsRoot(domainName, workflowName), "evaluation");
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetResultsRoot(string domainName, string workflowName)
        {
            var path = Path.Combine(GetDomainRoot(domainName), "results", workflowName);
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetMetricsResultsRoot(string domainName, string workflowName)
        {
            var path = Path.Combine(GetResultsRoot(domainName, workflowName), "metrics");
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetTrainingResultsRoot(string domainName, string workflowName)
        {
            var path = Path.Combine(GetResultsRoot(domainName, workflowName), "training");
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetInspectionResultsRoot(string domainName, string workflowName)
        {
            var path = Path.Combine(GetResultsRoot(domainName, workflowName), "inspection");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
