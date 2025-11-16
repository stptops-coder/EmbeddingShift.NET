using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Core.Stats;
using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// Simple ingestion workflow that walks a directory of text files,
    /// counts files, bytes and lines and exposes them as metrics.
    /// This is intended as a first real-world style ingest workflow
    /// that can later be connected to preprocessing and evaluation.
    /// </summary>
    public sealed class TextFileIngestWorkflow : IWorkflow
    {
        private readonly string _inputDirectory;
        private readonly string _searchPattern;
        private readonly bool _recursive;

        public string Name => "TextFileIngest";

        /// <summary>
        /// Creates a new text file ingest workflow.
        /// </summary>
        public TextFileIngestWorkflow(
            string inputDirectory,
            string searchPattern = "*.txt",
            bool recursive = true)
        {
            _inputDirectory = inputDirectory ?? throw new ArgumentNullException(nameof(inputDirectory));
            _searchPattern = string.IsNullOrWhiteSpace(searchPattern) ? "*.txt" : searchPattern;
            _recursive = recursive;
        }

        public Task<WorkflowResult> RunAsync(IStatsCollector stats, CancellationToken ct = default)
        {
            if (!Directory.Exists(_inputDirectory))
            {
                var notes =
                    $"ERROR DirectoryNotFound: Input directory does not exist: '{_inputDirectory}'. " +
                    $"Pattern: '{_searchPattern}', recursive: {_recursive}.";

                return Task.FromResult(new WorkflowResult(
                    Success: false,
                    Metrics: null,
                    Notes: notes));
            }

            long fileCount = 0;
            long totalBytes = 0;
            long totalLines = 0;

            var searchOption = _recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(_inputDirectory, _searchPattern, searchOption);

            using (stats.TrackStep("Ingest"))
            {
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();

                    fileCount++;

                    var info = new FileInfo(file);
                    totalBytes += info.Length;

                    // Lines are approximate but good enough for ingest stats
                    foreach (var _ in File.ReadLines(file))
                    {
                        ct.ThrowIfCancellationRequested();
                        totalLines++;
                    }
                }
            }

            var metrics = new Dictionary<string, double>
            {
                ["ingest.files"]      = fileCount,
                ["ingest.totalBytes"] = totalBytes,
                ["ingest.totalLines"] = totalLines
            };

            var successNotes =
                $"Ingested {fileCount} files from '{_inputDirectory}' " +
                $"(pattern: '{_searchPattern}', recursive: {_recursive}).";

            return Task.FromResult(new WorkflowResult(
                Success: true,
                Metrics: metrics,
                Notes: successNotes));
        }
    }
}
