using System.Globalization;

namespace EmbeddingShift.Workflows.Eval;

/// <summary>
/// Small, domain-neutral acceptance gate for CLI / automation.
/// Intended to keep regressions from slipping in when running:
///   eval <dataset> --baseline
/// </summary>
public sealed class EvalAcceptanceGate
{
    public sealed record GateCheck(string MetricKeyDelta, string DisplayName);

    public sealed record GateResult(
        bool Passed,
        double Epsilon,
        IReadOnlyList<string> Notes);

    private readonly IReadOnlyList<GateCheck> _checks;
    private readonly double _epsilon;

    private EvalAcceptanceGate(IReadOnlyList<GateCheck> checks, double epsilon)
    {
        _checks = checks ?? throw new ArgumentNullException(nameof(checks));
        _epsilon = epsilon;
    }

    /// <summary>
    /// Profiles:
    /// - "rank"         : NDCG/MRR delta must not regress (default)
    /// - "rank+cosine"  : additionally checks CosineSimilarityEvaluator.delta (useful for tiny datasets with 1 ref)
    /// </summary>
    public static EvalAcceptanceGate CreateFromProfile(string? profile, double epsilon = 1e-6)
    {
        var p = (profile ?? "rank").Trim().ToLowerInvariant();

        return p switch
        {
            "rank+cosine" or "rankcosine" or "rank-cosine" => CreateRankPlusCosine(epsilon),
            _ => CreateRankOnly(epsilon),
        };
    }

    public static EvalAcceptanceGate CreateRankOnly(double epsilon = 1e-6)
        => new EvalAcceptanceGate(
            new[]
            {
                new GateCheck("NdcgEvaluator.delta", "NDCG (delta)"),
                new GateCheck("MrrEvaluator.delta",  "MRR (delta)")
            },
            epsilon);

    public static EvalAcceptanceGate CreateRankPlusCosine(double epsilon = 1e-6)
        => new EvalAcceptanceGate(
            new[]
            {
                new GateCheck("NdcgEvaluator.delta",               "NDCG (delta)"),
                new GateCheck("MrrEvaluator.delta",                "MRR (delta)"),
                new GateCheck("CosineSimilarityEvaluator.delta",   "Cosine (delta)")
            },
            epsilon);

    public GateResult Evaluate(IReadOnlyDictionary<string, double>? metrics)
    {
        if (metrics is null)
        {
            return new GateResult(
                Passed: false,
                Epsilon: _epsilon,
                Notes: new[] { "MISSING: metrics dictionary is null" });
        }

        var notes = new List<string>();
        bool ok = true;

        foreach (var c in _checks)
        {
            if (!metrics.TryGetValue(c.MetricKeyDelta, out var delta))
            {
                ok = false;
                notes.Add($"MISSING: {c.MetricKeyDelta}");
                continue;
            }

            // Gate rule: delta must not be meaningfully negative.
            if (delta < -_epsilon)
            {
                ok = false;
                notes.Add(
                    $"FAIL: {c.MetricKeyDelta}={delta.ToString("0.####", CultureInfo.InvariantCulture)} (< -{_epsilon.ToString("0.####", CultureInfo.InvariantCulture)})");
            }
            else
            {
                notes.Add($"OK:   {c.MetricKeyDelta}={delta.ToString("0.####", CultureInfo.InvariantCulture)}");
            }
        }

        return new GateResult(Passed: ok, Epsilon: _epsilon, Notes: notes);
    }
}
