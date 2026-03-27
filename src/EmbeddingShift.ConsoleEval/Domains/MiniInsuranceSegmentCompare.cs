using System.Globalization;
using System.Text.Json;
using EmbeddingShift.Workflows;

namespace EmbeddingShift.ConsoleEval.Domains;

internal static class MiniInsuranceSegmentCompare
{
    private sealed record SegmentsFile(
        string? Metric,
        double Eps,
        string? VariantAPath,
        string? VariantBPath,
        string? BaselinePath,
        string? PosNegPath,
        Dictionary<string, string>? Decisions);

    public static int Run(string segmentsPath, string metric)
    {
        if (!File.Exists(segmentsPath))
            throw new FileNotFoundException("segments file not found", segmentsPath);

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var seg = JsonSerializer.Deserialize<SegmentsFile>(File.ReadAllText(segmentsPath), opts)
                  ?? throw new InvalidOperationException("Failed to parse segments file.");

        metric = ResolveMetric(metric, seg.Metric);

        var variantAPath = seg.VariantAPath ?? seg.BaselinePath
            ?? throw new InvalidOperationException("Segments file must contain VariantAPath (or legacy BaselinePath).");
        var variantBPath = seg.VariantBPath ?? seg.PosNegPath
            ?? throw new InvalidOperationException("Segments file must contain VariantBPath (or legacy PosNegPath).");

        if (!File.Exists(variantAPath))
            throw new FileNotFoundException("VariantA per-query file not found", variantAPath);
        if (!File.Exists(variantBPath))
            throw new FileNotFoundException("VariantB per-query file not found", variantBPath);

        var variantA = JsonSerializer.Deserialize<List<PerQueryEval>>(File.ReadAllText(variantAPath), opts) ?? new();
        var variantB = JsonSerializer.Deserialize<List<PerQueryEval>>(File.ReadAllText(variantBPath), opts) ?? new();

        var variantAById = variantA.ToDictionary(x => x.QueryId, StringComparer.OrdinalIgnoreCase);
        var variantBById = variantB.ToDictionary(x => x.QueryId, StringComparer.OrdinalIgnoreCase);
        int chooseA = 0, chooseB = 0, missing = 0, used = 0;

        double mapASum = 0, ndcgASum = 0;
        double mapBSum = 0, ndcgBSum = 0;
        double mapDecisionSum = 0, ndcgDecisionSum = 0;

        foreach (var (qid, a) in variantAById)
        {
            if (!variantBById.TryGetValue(qid, out var b))
            {
                missing++;
                continue;
            }

            if (!(seg.Decisions ?? new Dictionary<string, string>()).TryGetValue(qid, out var decision))
            {
                // If no decision exists, default to VariantA (conservative / compatibility with legacy skip behavior).
                decision = "VariantA";
            }

            var chooseVariantB = string.Equals(decision, "VariantB", StringComparison.OrdinalIgnoreCase)
                || string.Equals(decision, "ApplyShift", StringComparison.OrdinalIgnoreCase);

            if (chooseVariantB) chooseB++; else chooseA++;

            mapASum += a.Ap1; ndcgASum += a.Ndcg3;
            mapBSum += b.Ap1; ndcgBSum += b.Ndcg3;

            var chosen = chooseVariantB ? b : a;
            mapDecisionSum += chosen.Ap1;
            ndcgDecisionSum += chosen.Ndcg3;

            used++;
        }

        double mapA = used == 0 ? 0 : mapASum / used;
        double ndcgA = used == 0 ? 0 : ndcgASum / used;

        double mapB = used == 0 ? 0 : mapBSum / used;
        double ndcgB = used == 0 ? 0 : ndcgBSum / used;

        double mapDecision = used == 0 ? 0 : mapDecisionSum / used;
        double ndcgDecision = used == 0 ? 0 : ndcgDecisionSum / used;

        var primaryA = GetMetricValue(metric, mapA, ndcgA);
        var primaryB = GetMetricValue(metric, mapB, ndcgB);
        var primaryDecision = GetMetricValue(metric, mapDecision, ndcgDecision);
        var metricLabel = GetMetricLabel(metric);

        Console.WriteLine($"[segment-compare] segments = {segmentsPath}");
        Console.WriteLine($"[segment-compare] metric   = {metric}");
        Console.WriteLine($"[segment-compare] used={used}, missing={missing}, VariantA={chooseA}, VariantB={chooseB}");
        if (!string.IsNullOrWhiteSpace(seg.Metric))
            Console.WriteLine($"[segment-compare] file metric = {seg.Metric}");
        Console.WriteLine();
        Console.WriteLine($"Primary metric ({metricLabel})");
        Console.WriteLine($"  VariantA : {primaryA:0.000}");
        Console.WriteLine($"  VariantB : {primaryB:0.000}");
        Console.WriteLine($"  Decision : {primaryDecision:0.000}");
        Console.WriteLine();
        Console.WriteLine($"Primary delta vs VariantA ({metricLabel})");
        Console.WriteLine($"  VariantB : {(primaryB - primaryA):+0.000;-0.000;0.000}");
        Console.WriteLine($"  Decision : {(primaryDecision - primaryA):+0.000;-0.000;0.000}");
        Console.WriteLine();
        Console.WriteLine("KPI (avg over used cases)");
        Console.WriteLine($"  VariantA : MAP@1={mapA:0.000}, NDCG@3={ndcgA:0.000}");
        Console.WriteLine($"  VariantB : MAP@1={mapB:0.000}, NDCG@3={ndcgB:0.000}");
        Console.WriteLine($"  Decision : MAP@1={mapDecision:0.000}, NDCG@3={ndcgDecision:0.000}");
        Console.WriteLine();
        Console.WriteLine("Delta vs VariantA");
        Console.WriteLine($"  VariantB : MAP@1={(mapB - mapA):+0.000;-0.000;0.000}, NDCG@3={(ndcgB - ndcgA):+0.000;-0.000;0.000}");
        Console.WriteLine($"  Decision : MAP@1={(mapDecision - mapA):+0.000;-0.000;0.000}, NDCG@3={(ndcgDecision - ndcgA):+0.000;-0.000;0.000}");

        return 0;
    }

    private static string ResolveMetric(string? cliMetric, string? fileMetric)
    {
        var normalizedCli = string.IsNullOrWhiteSpace(cliMetric) ? null : cliMetric.Trim().ToLowerInvariant();
        var normalizedFile = string.IsNullOrWhiteSpace(fileMetric) ? null : fileMetric.Trim().ToLowerInvariant();

        var resolved = normalizedCli;
        if (string.IsNullOrWhiteSpace(resolved) || string.Equals(resolved, "ndcg@3", StringComparison.OrdinalIgnoreCase))
            resolved = normalizedFile ?? resolved;

        resolved ??= "ndcg@3";

        return resolved switch
        {
            "ndcg@3" => resolved,
            "map@1" => resolved,
            _ => throw new ArgumentException($"Unsupported metric '{resolved}'. Supported: ndcg@3, map@1")
        };
    }

    private static string GetMetricLabel(string metric)
        => metric switch
        {
            "map@1" => "MAP@1",
            _ => "NDCG@3"
        };

    private static double GetMetricValue(string metric, double map, double ndcg)
        => metric switch
        {
            "map@1" => map,
            _ => ndcg
        };
}
