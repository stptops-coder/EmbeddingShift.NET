using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Core.Stats;
using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Workflows
{
    /// <summary>
    /// Splits cleaned text files into character-based chunks and writes them
    /// to an output directory, preserving the relative structure.
    /// This is intentionally simple and can later be replaced or extended.
    /// </summary>
    public sealed class TextChunkingWorkflow : IWorkflow
    {
        private readonly string _inputDirectory;
        private readonly string _outputDirectory;
        private readonly string _searchPattern;
        private readonly bool _recursive;
        private readonly int _maxCharsPerChunk;

        public string Name => "TextChunking";

        public TextChunkingWorkflow(
            string inputDirectory,
            string outputDirectory,
            int maxCharsPerChunk,
            string searchPattern = "*.txt",
            bool recursive = true)
        {
            if (maxCharsPerChunk <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCharsPerChunk), "maxCharsPerChunk must be > 0.");

            _inputDirectory   = inputDirectory   ?? throw new ArgumentNullException(nameof(inputDirectory));
            _outputDirectory  = outputDirectory  ?? throw new ArgumentNullException(nameof(outputDirectory));
            _searchPattern    = string.IsNullOrWhiteSpace(searchPattern) ? "*.txt" : searchPattern;
            _recursive        = recursive;
            _maxCharsPerChunk = maxCharsPerChunk;
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

            long sourceFiles      = 0;
            long totalCharsSource = 0;
            long totalChunks      = 0;

            var searchOption = _recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(_inputDirectory, _searchPattern, searchOption);

            using (stats.TrackStep("Chunking"))
            {
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();

                    sourceFiles++;

                    var text = File.ReadAllText(file);
                    totalCharsSource += text.Length;

                    var relPath   = Path.GetRelativePath(_inputDirectory, file);
                    var baseName  = Path.GetFileNameWithoutExtension(relPath);
                    var relFolder = Path.GetDirectoryName(relPath) ?? string.Empty;

                    var outDir = Path.Combine(_outputDirectory, relFolder);
                    Directory.CreateDirectory(outDir);

                    var chunks = CreateChunks(text, _maxCharsPerChunk);
                    int index = 0;
                    foreach (var chunk in chunks)
                    {
                        ct.ThrowIfCancellationRequested();

                        var chunkFileName = $"{baseName}_chunk{index:D3}.txt";
                        var chunkPath = Path.Combine(outDir, chunkFileName);
                        File.WriteAllText(chunkPath, chunk);

                        totalChunks++;
                        index++;
                    }
                }
            }

            var metrics = new Dictionary<string, double>
            {
                ["chunk.sourceFiles"]      = sourceFiles,
                ["chunk.totalCharsSource"] = totalCharsSource,
                ["chunk.totalChunks"]      = totalChunks
            };

            var notesSummary =
                $"Chunked {sourceFiles} files from '{_inputDirectory}' into '{_outputDirectory}' " +
                $"with max {_maxCharsPerChunk} chars per chunk.";

            return Task.FromResult(new WorkflowResult(
                Success: true,
                Metrics: metrics,
                Notes: notesSummary));
        }

        private static IEnumerable<string> CreateChunks(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text))
                yield break;

            var sb = new StringBuilder(maxChars);
            foreach (var ch in text)
            {
                sb.Append(ch);
                if (sb.Length >= maxChars)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
            }

            if (sb.Length > 0)
            {
                yield return sb.ToString();
            }
        }
    }
}
