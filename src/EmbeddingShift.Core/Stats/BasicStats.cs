using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace EmbeddingShift.Core.Stats
{
    public sealed class InMemoryStatsSink : IStatsSink
    {
        private readonly ConcurrentQueue<StatEvent> _events = new();

        public IReadOnlyCollection<StatEvent> Events => _events.ToArray();

        public void Write(StatEvent e) => _events.Enqueue(e);
    }

    public sealed class BasicStatsCollector : IStatsCollector
    {
        private readonly IStatsSink _sink;
        private int _runStarted;

        public Guid RunId { get; } = Guid.NewGuid();

        public BasicStatsCollector(IStatsSink sink) => _sink = sink;

        public void StartRun(string name, string? meta = null)
        {
            if (System.Threading.Interlocked.Exchange(ref _runStarted, 1) == 1) return;
            _sink.Write(new StatEvent(DateTimeOffset.UtcNow, StatEventKind.RunStarted, name, Meta: meta));
        }

        public void EndRun(string? meta = null)
        {
            _sink.Write(new StatEvent(DateTimeOffset.UtcNow, StatEventKind.RunEnded, "Run", Meta: meta));
        }

        public IDisposable TrackStep(string name, string? meta = null) =>
            new StepScope(name, meta, _sink);

        public void RecordExternal(string opName, int tokensIn = 0, int tokensOut = 0, string? meta = null)
        {
            _sink.Write(new StatEvent(
                DateTimeOffset.UtcNow,
                StatEventKind.ExternalOp,
                opName,
                TokensIn: tokensIn,
                TokensOut: tokensOut,
                Meta: meta));
        }

        public void RecordMetric(string name, double value, string? meta = null)
        {
            _sink.Write(new StatEvent(
                DateTimeOffset.UtcNow,
                StatEventKind.Metric,
                name,
                Value: value,
                Meta: meta));
        }

        public void RecordError(string name, Exception ex, string? meta = null)
        {
            _sink.Write(new StatEvent(
                DateTimeOffset.UtcNow,
                StatEventKind.Error,
                name,
                ErrorMessage: ex.Message,
                Meta: meta));
        }

        private sealed class StepScope : IDisposable
        {
            private readonly string _name;
            private readonly string? _meta;
            private readonly IStatsSink _sink;
            private readonly Stopwatch _sw = Stopwatch.StartNew();
            private int _disposed;

            public StepScope(string name, string? meta, IStatsSink sink)
            {
                _name = name;
                _meta = meta;
                _sink = sink;

                _sink.Write(new StatEvent(
                    DateTimeOffset.UtcNow,
                    StatEventKind.StepStarted,
                    _name,
                    Meta: _meta));
            }

            public void Dispose()
            {
                if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 1) return;

                _sw.Stop();
                _sink.Write(new StatEvent(
                    DateTimeOffset.UtcNow,
                    StatEventKind.StepEnded,
                    _name,
                    Duration: _sw.Elapsed,
                    Meta: _meta));
            }
        }
    }

    public static class StatsReport
    {
        public static string ToMarkdown(IEnumerable<StatEvent> events)
        {
            var evts = events.OrderBy(e => e.At).ToArray();

            var total = evts.Where(e => e.Kind == StatEventKind.StepEnded)
                .Sum(e => e.Duration?.TotalMilliseconds ?? 0);

            var steps = evts.Where(e => e.Kind == StatEventKind.StepStarted)
                .Select(e => e.Name)
                .Distinct()
                .ToArray();

            var errors = evts.Where(e => e.Kind == StatEventKind.Error).ToArray();
            var ext = evts.Where(e => e.Kind == StatEventKind.ExternalOp).ToArray();

            var tokensIn = ext.Sum(e => e.TokensIn ?? 0);
            var tokensOut = ext.Sum(e => e.TokensOut ?? 0);

            var sb = new StringBuilder();
            sb.AppendLine("# Run Statistics");
            sb.AppendLine();
            sb.AppendLine($"- **Steps**: {steps.Length}");
            sb.AppendLine($"- **Total Step Time**: {total:N0} ms");
            sb.AppendLine($"- **External Ops**: {ext.Length} (tokens in: {tokensIn}, out: {tokensOut})");
            sb.AppendLine($"- **Errors**: {errors.Length}");
            sb.AppendLine();

            if (steps.Length > 0)
            {
                sb.AppendLine("## Step Durations");
                sb.AppendLine("| Step | Duration (ms) |");
                sb.AppendLine("|---|---:|");
                foreach (var e in evts.Where(x => x.Kind == StatEventKind.StepEnded))
                    sb.AppendLine($"| {e.Name} | {e.Duration?.TotalMilliseconds:N0} |");
                sb.AppendLine();
            }

            if (ext.Length > 0)
            {
                sb.AppendLine("## External Operations");
                sb.AppendLine("| Op | Tokens In | Tokens Out | Time |");
                sb.AppendLine("|---|---:|---:|---|");
                foreach (var e in ext)
                    sb.AppendLine($"| {e.Name} | {e.TokensIn} | {e.TokensOut} | {e.At:HH:mm:ss} |");
                sb.AppendLine();
            }

            if (errors.Length > 0)
            {
                sb.AppendLine("## Errors");
                foreach (var e in errors)
                    sb.AppendLine($"- **{e.Name}**: {e.ErrorMessage}");
            }

            return sb.ToString();
        }
    }
}
