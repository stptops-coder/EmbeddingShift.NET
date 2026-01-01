using System.Text.Json;

namespace EmbeddingShift.Workflows.Eval;

/// <summary>
/// Persists a small machine-readable summary of the acceptance gate decision next to a run/eval result directory.
/// Best-effort: failures to write must never break runs.
/// </summary>
public static class EvalAcceptanceGateManifest
{
    public const string FileName = "acceptance_gate.json";

    public static async Task<bool> TryWriteAsync(
        DatasetEvalResult evalResult,
        string? gateProfile,
        EvalAcceptanceGate.GateResult gateResult,
        CancellationToken ct = default)
    {
        if (evalResult is null) throw new ArgumentNullException(nameof(evalResult));

        var resultsPath = evalResult.ResultsPath;
        if (string.IsNullOrWhiteSpace(resultsPath))
            return false;

        try
        {
            Directory.CreateDirectory(resultsPath);

            var profile = string.IsNullOrWhiteSpace(gateProfile) ? "rank" : gateProfile.Trim();

            var payload = new AcceptanceGateManifest(
                Dataset: evalResult.Dataset,
                RunId: evalResult.RunId,
                CreatedUtc: DateTimeOffset.UtcNow,
                GateProfile: profile,
                Passed: gateResult.Passed,
                Epsilon: gateResult.Epsilon,
                Notes: gateResult.Notes?.ToArray() ?? Array.Empty<string>(),
                Metrics: evalResult.Metrics is null ? null : new Dictionary<string, double>(evalResult.Metrics),
                RefsManifestPath: evalResult.RefsManifestPath,
                ResultsPath: resultsPath);

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var outPath = Path.Combine(resultsPath, FileName);
            await File.WriteAllTextAsync(outPath, json, ct).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed record AcceptanceGateManifest(
    string Dataset,
    Guid? RunId,
    DateTimeOffset CreatedUtc,
    string GateProfile,
    bool Passed,
    double Epsilon,
    IReadOnlyList<string> Notes,
    IReadOnlyDictionary<string, double>? Metrics,
    string? RefsManifestPath,
    string ResultsPath);

}
