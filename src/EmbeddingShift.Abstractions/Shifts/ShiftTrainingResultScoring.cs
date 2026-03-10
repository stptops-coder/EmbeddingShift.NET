using System;

namespace EmbeddingShift.Abstractions.Shifts;

/// <summary>
/// Central scoring rules for <see cref="ShiftTrainingResult"/> selection.
/// Legacy First/First+Delta runs keep using their historical improvements.
/// Newer training modes (for example PosNeg) can persist an explicit selection score.
/// </summary>
public static class ShiftTrainingResultScoring
{
    private const double Epsilon = 1e-12;

    public static double GetPreferredScore(ShiftTrainingResult result)
    {
        if (result is null)
            throw new ArgumentNullException(nameof(result));

        if (result.SelectionScore.HasValue)
            return result.SelectionScore.Value;

        if (Math.Abs(result.ImprovementFirstPlusDelta) >= Epsilon)
            return result.ImprovementFirstPlusDelta;

        return result.ImprovementFirst;
    }

    public static string GetPreferredScoreSource(ShiftTrainingResult result)
    {
        if (result is null)
            throw new ArgumentNullException(nameof(result));

        if (result.SelectionScore.HasValue)
            return "selection-score";

        if (Math.Abs(result.ImprovementFirstPlusDelta) >= Epsilon)
            return "legacy:first+delta";

        return "legacy:first";
    }
}
