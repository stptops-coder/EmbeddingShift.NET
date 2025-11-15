using System;
using System.Collections.Generic;

namespace EmbeddingShift.Core.Stats
{
    public enum StatEventKind
    {
        RunStarted,
        RunEnded,
        StepStarted,
        StepEnded,
        ExternalOp,
        Metric,
        Error
    }

    public sealed record StatEvent(
        DateTimeOffset At,
        StatEventKind Kind,
        string Name,
        TimeSpan? Duration = null,
        double? Value = null,
        int? TokensIn = null,
        int? TokensOut = null,
        string? Meta = null,
        string? ErrorMessage = null
    );

    public interface IStatsSink
    {
        void Write(StatEvent e);
    }

    public interface IStatsCollector
    {
        Guid RunId { get; }

        void StartRun(string name, string? meta = null);
        void EndRun(string? meta = null);

        IDisposable TrackStep(string name, string? meta = null);
        void RecordExternal(string opName, int tokensIn = 0, int tokensOut = 0, string? meta = null);
        void RecordMetric(string name, double value, string? meta = null);
        void RecordError(string name, Exception ex, string? meta = null);
    }
}
