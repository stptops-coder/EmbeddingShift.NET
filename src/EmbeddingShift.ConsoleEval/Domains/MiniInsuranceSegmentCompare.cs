using System.Globalization;
using System.Text.Json;
using EmbeddingShift.Workflows;

namespace EmbeddingShift.ConsoleEval.Domains;

internal static class MiniInsuranceSegmentCompare
{
    private sealed record SegmentsFile(
        string? Metric,
        double? Eps,
        string? BaselinePath,
        string? PosNegPath,
        string? PrimaryPath,
        string? SecondaryPath,
        string? PrimaryLabel,
        string? SecondaryLabel,
        Dictionary<string, string>? Decisions);

    public static int Run(string segmentsPath, string metric)
    {
        if (!File.Exists(segmentsPath))
            throw new FileNotFoundException("segments file not found", segmentsPath);

        metric = (metric ?? "ndcg@3").ToLowerInvariant();

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var seg = JsonSerializer.Deserialize<SegmentsFile>(File.ReadAllText(segmentsPath), opts)
                  ?? throw new InvalidOperationException("Failed to parse segments file.");

        var primaryPath = FirstNonEmpty(seg.PrimaryPath, seg.BaselinePath)
            ?? throw new InvalidOperationException("Segments file must contain PrimaryPath or BaselinePath.");
        var secondaryPath = FirstNonEmpty(seg.SecondaryPath, seg.PosNegPath)
            ?? throw new InvalidOperationException("Segments file must contain SecondaryPath or PosNegPath.");

        var primaryLabel = FirstNonEmpty(seg.PrimaryLabel, string.IsNullOrWhiteSpace(seg.BaselinePath) ? null : "Baseline", "Primary")!;
        var secondaryLabel = FirstNonEmpty(seg.SecondaryLabel, string.IsNullOrWhiteSpace(seg.PosNegPath) ? null : "PosNeg", "Secondary")!;
        var decisions = seg.Decisions ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(primaryPath))
            throw new FileNotFoundException($"{primaryLabel} per-query file not found", primaryPath);
        if (!File.Exists(secondaryPath))
            throw new FileNotFoundException($"{secondaryLabel} per-query file not found", secondaryPath);

        var primary = JsonSerializer.Deserialize<List<PerQueryEval>>(File.ReadAllText(primaryPath), opts) ?? new();
        var secondary = JsonSerializer.Deserialize<List<PerQueryEval>>(File.ReadAllText(secondaryPath), opts) ?? new();

        var primaryById = primary.ToDictionary(x => x.QueryId, StringComparer.OrdinalIgnoreCase);
        var secondaryById = secondary.ToDictionary(x => x.QueryId, StringComparer.OrdinalIgnoreCase);
        int selectPrimary = 0, selectSecondary = 0, missing = 0, used = 0;

        double mapPrimarySum = 0, ndcgPrimarySum = 0;
        double mapSecondarySum = 0, ndcgSecondarySum = 0;
        double mapMixedSum = 0, ndcgMixedSum = 0;

        foreach (var (qid, p1) in primaryById)
        {
            if (!secondaryById.TryGetValue(qid, out var p2))
            {
                missing++;
                continue;
            }

            if (!decisions.TryGetValue(qid, out var decision))
            {
                decision = primaryLabel;
            }

            var useSecondary = IsSecondaryDecision(decision, secondaryLabel);
            if (useSecondary) selectSecondary++; else selectPrimary++;

            mapPrimarySum += p1.Ap1; ndcgPrimarySum += p1.Ndcg3;
            mapSecondarySum += p2.Ap1; ndcgSecondarySum += p2.Ndcg3;

            var chosen = useSecondary ? p2 : p1;
            mapMixedSum += chosen.Ap1;
            ndcgMixedSum += chosen.Ndcg3;

            used++;
        }

        double mapPrimary = used == 0 ? 0 : mapPrimarySum / used;
        double ndcgPrimary = used == 0 ? 0 : ndcgPrimarySum / used;

        double mapSecondary = used == 0 ? 0 : mapSecondarySum / used;
        double ndcgSecondary = used == 0 ? 0 : ndcgSecondarySum / used;

        double mapMixed = used == 0 ? 0 : mapMixedSum / used;
        double ndcgMixed = used == 0 ? 0 : ndcgMixedSum / used;

        Console.WriteLine($"[segment-compare] segments = {segmentsPath}");
        Console.WriteLine($"[segment-compare] metric   = {metric}");
        Console.WriteLine($"[segment-compare] used={used}, missing={missing}, {primaryLabel}={selectPrimary}, {secondaryLabel}={selectSecondary}");
        Console.WriteLine();
        Console.WriteLine("KPI (avg over used cases)");
        Console.WriteLine($"  {primaryLabel,-10}: MAP@1={mapPrimary:0.000}, NDCG@3={ndcgPrimary:0.000}");
        Console.WriteLine($"  {secondaryLabel,-10}: MAP@1={mapSecondary:0.000}, NDCG@3={ndcgSecondary:0.000}");
        Console.WriteLine($"  DecisionMix: MAP@1={mapMixed:0.000}, NDCG@3={ndcgMixed:0.000}");
        Console.WriteLine();
        Console.WriteLine($"Delta vs {primaryLabel}");
        Console.WriteLine($"  {secondaryLabel,-10}: MAP@1={(mapSecondary - mapPrimary):+0.000;-0.000;0.000}, NDCG@3={(ndcgSecondary - ndcgPrimary):+0.000;-0.000;0.000}");
        Console.WriteLine($"  DecisionMix: MAP@1={(mapMixed - mapPrimary):+0.000;-0.000;0.000}, NDCG@3={(ndcgMixed - ndcgPrimary):+0.000;-0.000;0.000}");

        return 0;
    }

    private static bool IsSecondaryDecision(string? decision, string secondaryLabel)
    {
        if (string.IsNullOrWhiteSpace(decision))
            return false;

        var normalized = Normalize(decision);
        if (normalized.Length == 0)
            return false;

        return normalized is "applyshift" or "usesecondary" or "secondary" or "variantb" or "useb"
            || normalized == Normalize(secondaryLabel);
    }

    private static string Normalize(string? value)
        => new(value?.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray() ?? Array.Empty<char>());

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
