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
    /// Generic text cleanup workflow:
    /// - reads .txt files from an input directory (optionally recursive)
    /// - trims whitespace and removes empty lines
    /// - optionally lowercases all text
    /// - writes cleaned files to an output directory (same relative structure)
    /// </summary>
    public sealed class TextCleanupWorkflow : IWorkflow
    {
        private readonly string _inputDirectory;
        private readonly string _outputDirectory;
        private readonly string _searchPattern;
        private readonly bool _recursive;
        private readonly bool _lowercase;

        public string Name => "TextCleanup";

        public TextCleanupWorkflow(
            string inputDirectory,
            string outputDirectory,
            string searchPattern = "*.txt",
            bool recursive = true,
            bool lowercase = false)
        {
            _inputDirectory = inputDirectory ?? throw new ArgumentNullException(nameof(inputDirectory));
            _outputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
            _searchPattern = string.IsNullOrWhiteSpace(searchPattern) ? "*.txt" : searchPattern;
            _recursive = recursive;
            _lowercase = lowercase;
        }

        public Task<WorkflowResult> RunAsync(IStatsCollector stats, CancellationToken ct = default)
        {
            if (!Directory.Exists(_inputDirectory))
            {
                var notes = $"ERROR: Input directory does not exist: '{_inputDirectory}'.";
                return Task.FromResult(new WorkflowResult(
                    Success: false,
                    Metrics: null,
                    Notes: notes));
            }

            Directory.CreateDirectory(_outputDirectory);

            long fileCount = 0;
            long totalLinesIn = 0;
            long totalLinesOut = 0;

            var searchOption = _recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(_inputDirectory, _searchPattern, searchOption);

            using (stats.TrackStep("Preprocessing"))
            {
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();

                    fileCount++;

                    var relPath = Path.GetRelativePath(_inputDirectory, file);
                    var outFile = Path.Combine(_outputDirectory, relPath);
                    var outDir = Path.GetDirectoryName(outFile);
                    if (!string.IsNullOrEmpty(outDir))
                    {
                        Directory.CreateDirectory(outDir);
                    }

                    var cleanedLines = new List<string>();

                    foreach (var line in File.ReadLines(file))
                    {
                        ct.ThrowIfCancellationRequested();

                        totalLinesIn++;

                        var t = line.Trim();
                        if (t.Length == 0)
                        {
                            continue; // skip empty lines
                        }

                        if (_lowercase)
                        {
                            t = t.ToLowerInvariant();
                        }

                        cleanedLines.Add(t);
                        totalLinesOut++;
                    }

                    File.WriteAllLines(outFile, cleanedLines);
                }
            }

            var metrics = new Dictionary<string, double>
            {
                ["pre.files"]          = fileCount,
                ["pre.totalLinesIn"]   = totalLinesIn,
                ["pre.totalLinesOut"]  = totalLinesOut
            };

            var notesSummary =
                $"Cleaned {fileCount} files from '{_inputDirectory}' into '{_outputDirectory}' " +
                $"(pattern: '{_searchPattern}', recursive: {_recursive}, lowercase: {_lowercase}).";

            return Task.FromResult(new WorkflowResult(
                Success: true,
                Metrics: metrics,
                Notes: notesSummary));
        }
    }
}
