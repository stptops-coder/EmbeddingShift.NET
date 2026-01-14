using System.Globalization;
using System.Text.Json;

namespace EmbeddingShift.ConsoleEval.Domains;

internal static class MiniInsuranceSegmentCompare
{
    private sealed record PerQueryEval(
        string QueryId,
        string RelevantDocId,
        int Rank,
        double Ap1,
        double Ndcg3,
        string? TopDocId,
        double TopScore);

    private sealed record SegmentsFile(
        string Metric,
        double Eps,
        string BaselinePath,
        string PosNegPath,
        Dictionary<string, string> Decisions);

    public static int Run(string segmentsPath, string metric)
    {
        if (!File.Exists(segmentsPath))
            throw new FileNotFoundException("segments file not found", segmentsPath);

        metric = (metric ?? "ndcg@3").ToLowerInvariant();

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var seg = JsonSerializer.Deserialize<SegmentsFile>(File.ReadAllText(segmentsPath), opts)
                  ?? throw new InvalidOperationException("Failed to parse segments file.");

        if (!File.Exists(seg.BaselinePath))
            throw new FileNotFoundException("Baseline per-query file not found", seg.BaselinePath);
        if (!File.Exists(seg.PosNegPath))
            throw new FileNotFoundException("PosNeg per-query file not found", seg.PosNegPath);

        var baseline = JsonSerializer.Deserialize<List<PerQueryEval>>(File.ReadAllText(seg.BaselinePath), opts) ?? new();
        var posneg = JsonSerializer.Deserialize<List<PerQueryEval>>(File.ReadAllText(seg.PosNegPath), opts) ?? new();

        var baseById = baseline.ToDictionary(x => x.QueryId, StringComparer.OrdinalIgnoreCase);
        var posById = posneg.ToDictionary(x => x.QueryId, StringComparer.OrdinalIgnoreCase);

        double GetMetric(PerQueryEval e) => metric switch
        {
            "ndcg@3" => e.Ndcg3,
            "map@1" => e.Ap1,
            _ => throw new ArgumentException($"Unsupported metric: {metric}")
        };

        int apply = 0, skip = 0, missing = 0, used = 0;

        double mapBaseSum = 0, ndcgBaseSum = 0;
        double mapPosSum = 0, ndcgPosSum = 0;
        double mapSegSum = 0, ndcgSegSum = 0;

        foreach (var (qid, b) in baseById)
        {
            if (!posById.TryGetValue(qid, out var p))
            {
                missing++;
                continue;
            }

            if (!seg.Decisions.TryGetValue(qid, out var decision))
            {
                // If no decision exists, default to SkipShift (conservative)
                decision = "SkipShift";
            }

            var usePos = string.Equals(decision, "ApplyShift", StringComparison.OrdinalIgnoreCase);
            if (usePos) apply++; else skip++;

            mapBaseSum += b.Ap1; ndcgBaseSum += b.Ndcg3;
            mapPosSum += p.Ap1; ndcgPosSum += p.Ndcg3;

            var chosen = usePos ? p : b;
            mapSegSum += chosen.Ap1;
            ndcgSegSum += chosen.Ndcg3;

            used++;
        }

        double mapBase = used == 0 ? 0 : mapBaseSum / used;
        double ndcgBase = used == 0 ? 0 : ndcgBaseSum / used;

        double mapPos = used == 0 ? 0 : mapPosSum / used;
        double ndcgPos = used == 0 ? 0 : ndcgPosSum / used;

        double mapSeg = used == 0 ? 0 : mapSegSum / used;
        double ndcgSeg = used == 0 ? 0 : ndcgSegSum / used;

        Console.WriteLine($"[segment-compare] segments = {segmentsPath}");
        Console.WriteLine($"[segment-compare] metric   = {metric}");
        Console.WriteLine($"[segment-compare] used={used}, missing={missing}, ApplyShift={apply}, SkipShift={skip}");
        Console.WriteLine();
        Console.WriteLine("KPI (avg over used cases)");
        Console.WriteLine($"  Baseline : MAP@1={mapBase:0.000}, NDCG@3={ndcgBase:0.000}");
        Console.WriteLine($"  PosNeg   : MAP@1={mapPos:0.000}, NDCG@3={ndcgPos:0.000}");
        Console.WriteLine($"  Segmented: MAP@1={mapSeg:0.000}, NDCG@3={ndcgSeg:0.000}");
        Console.WriteLine();
        Console.WriteLine("Delta vs Baseline");
        Console.WriteLine($"  PosNeg   : MAP@1={(mapPos - mapBase):+0.000;-0.000;0.000}, NDCG@3={(ndcgPos - ndcgBase):+0.000;-0.000;0.000}");
        Console.WriteLine($"  Segmented: MAP@1={(mapSeg - mapBase):+0.000;-0.000;0.000}, NDCG@3={(ndcgSeg - ndcgBase):+0.000;-0.000;0.000}");

        return 0;
    }
}
