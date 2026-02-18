using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddingShift.Core.Stats
{
    /// <summary>
    /// Persistent representation of a single workflow run, including metrics.
    /// </summary>
    public sealed record WorkflowRunArtifact(
        string RunId,
        string WorkflowName,
        DateTimeOffset StartedUtc,
        DateTimeOffset FinishedUtc,
        bool Success,
        IReadOnlyDictionary<string, double> Metrics,
        string Notes);

    /// <summary>
    /// Abstraction for persisting workflow run artifacts.
    /// </summary>
    public interface IRunRepository
    {
        Task SaveAsync(WorkflowRunArtifact artifact, CancellationToken ct = default);
    }

    /// <summary>
    /// File-based implementation of <see cref="IRunRepository"/> that stores
    /// each run as a JSON file under a configured root directory.
    /// </summary>
    public sealed class FileRunRepository : IRunRepository
    {
        private readonly string _rootDirectory;

        public FileRunRepository(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("Root directory must be provided.", nameof(rootDirectory));

            _rootDirectory = rootDirectory;
        }

        public async Task SaveAsync(WorkflowRunArtifact artifact, CancellationToken ct = default)
        {
            if (artifact is null) throw new ArgumentNullException(nameof(artifact));

            var safeWorkflowName = SanitizePathSegment(artifact.WorkflowName);
            var runFolder = Path.Combine(_rootDirectory, "_repo", safeWorkflowName, artifact.RunId);

            Directory.CreateDirectory(runFolder);

            var path = Path.Combine(runFolder, "run.json");

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var metricsSource = artifact.Metrics ?? new Dictionary<string, double>();
            var metrics = metricsSource is Dictionary<string, double> dict
                ? dict
                : new Dictionary<string, double>(metricsSource);

            var serializableArtifact = artifact with { Metrics = metrics };

            var json = JsonSerializer.Serialize(serializableArtifact, options);

            await File.WriteAllTextAsync(path, json, Encoding.UTF8, ct).ConfigureAwait(false);
        }

        private static string SanitizePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(c, '_');
            }

            return value;
        }
    }
}
