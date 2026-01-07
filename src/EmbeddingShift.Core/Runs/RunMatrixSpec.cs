using System.Text.Json;

namespace EmbeddingShift.Core.Runs;

/// <summary>
/// JSON specification for running multiple CLI variants as a batch (a "matrix") and optionally
/// post-processing the produced run artifacts (compare/promote).
/// </summary>
public sealed record RunMatrixSpec(
    IReadOnlyList<RunMatrixVariant> Variants,
    RunMatrixAfter? After = null,
    bool StopOnFailure = true)
{
    public static RunMatrixSpec Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Spec path is required.", nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException($"Matrix spec not found: {path}", path);

        var json = File.ReadAllText(path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var spec = JsonSerializer.Deserialize<RunMatrixSpec>(json, options)
                   ?? throw new InvalidOperationException("Failed to deserialize matrix spec (null).");

        if (spec.Variants is null || spec.Variants.Count == 0)
            throw new InvalidOperationException("Matrix spec must contain at least one variant.");

        return spec;
    }
}

public sealed record RunMatrixVariant(
    string Name,
    string[] Args,
    Dictionary<string, string>? Env = null,
    int? TimeoutSeconds = null)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "(unnamed)" : Name.Trim();
}

public sealed record RunMatrixAfter(
    string CompareMetric = "ndcg@3",
    int Top = 10,
    bool WriteCompare = true,
    bool PromoteBest = false,
    bool OpenOutput = false,
    string? DomainKey = null);
