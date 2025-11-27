using System;
using System.IO;
using System.Text;
using System.Text.Json;
using EmbeddingShift.Abstractions.Shifts;

namespace EmbeddingShift.ConsoleEval.Repositories;

/// <summary>
/// File-system based implementation of <see cref="IShiftTrainingResultRepository"/>.
/// 
/// Layout convention:
///   rootDirectory/
///     {workflowName}-training_{timestamp}/
///       shift-training-result.json
///
/// The exact JSON shape is defined by <see cref="ShiftTrainingResult"/>.
/// </summary>
public sealed class FileSystemShiftTrainingResultRepository : IShiftTrainingResultRepository
{
    private readonly string _rootDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Encoding _utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Creates a new file-system based shift training result repository.
    /// </summary>
    /// <param name="rootDirectory">
    /// Root directory under which training results should be stored.
    /// This directory will be created if it does not exist.
    /// </param>
    public FileSystemShiftTrainingResultRepository(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("Root directory must not be null or empty.", nameof(rootDirectory));

        _rootDirectory = rootDirectory;
        Directory.CreateDirectory(_rootDirectory);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }

    /// <inheritdoc />
    public void Save(ShiftTrainingResult result)
    {
        if (result is null)
            throw new ArgumentNullException(nameof(result));

        if (string.IsNullOrWhiteSpace(result.WorkflowName))
            throw new ArgumentException("WorkflowName must be set on the training result.", nameof(result));

        // Ensure we have a sensible timestamp.
        var createdUtc = result.CreatedUtc == default
            ? DateTime.UtcNow
            : result.CreatedUtc;

        // Directory layout: {workflowName}-training_yyyyMMdd_HHmmss_fff
        var stamp = createdUtc.ToString("yyyyMMdd_HHmmss_fff");
        var directoryName = $"{result.WorkflowName}-training_{stamp}";
        var targetDirectory = Path.Combine(_rootDirectory, directoryName);

        Directory.CreateDirectory(targetDirectory);

        var jsonPath = Path.Combine(targetDirectory, "shift-training-result.json");
        var json = JsonSerializer.Serialize(result with { CreatedUtc = createdUtc }, _jsonOptions);

        File.WriteAllText(jsonPath, json, _utf8NoBom);
    }

    /// <inheritdoc />
    public ShiftTrainingResult? LoadLatest(string workflowName)
    {
        if (string.IsNullOrWhiteSpace(workflowName))
            throw new ArgumentException("Workflow name must not be null or empty.", nameof(workflowName));

        if (!Directory.Exists(_rootDirectory))
            return null;

        // Pattern: {workflowName}-training_*
        var prefix = $"{workflowName}-training_";
        var candidates = Directory.GetDirectories(_rootDirectory, $"{workflowName}-training_*", SearchOption.TopDirectoryOnly);

        if (candidates.Length == 0)
            return null;

        Array.Sort(candidates, StringComparer.Ordinal);
        Array.Reverse(candidates);

        foreach (var dir in candidates)
        {
            var jsonPath = Path.Combine(dir, "shift-training-result.json");
            if (!File.Exists(jsonPath))
                continue;

            try
            {
                var json = File.ReadAllText(jsonPath, _utf8NoBom);
                var result = JsonSerializer.Deserialize<ShiftTrainingResult>(json, _jsonOptions);
                if (result != null)
                    return result;
            }
            catch
            {
                // Ignore malformed entries and continue with older ones.
            }
        }

        return null;
    }
}
