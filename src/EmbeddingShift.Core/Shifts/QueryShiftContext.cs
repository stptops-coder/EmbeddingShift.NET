using System;
using System.Threading;

namespace EmbeddingShift.Core.Shifts
{
    /// <summary>
    /// Ambient context for per-query shifting.
    /// The workflow sets the current QueryId while evaluating a single query.
    /// Shifts can use this to apply query-specific deltas.
    /// </summary>
    public static class QueryShiftContext
    {
        private static readonly AsyncLocal<string?> _currentQueryId = new AsyncLocal<string?>();

        public static string? CurrentQueryId => _currentQueryId.Value;

        public static IDisposable Push(string queryId)
        {
            if (string.IsNullOrWhiteSpace(queryId))
                throw new ArgumentException("QueryId must not be empty.", nameof(queryId));

            var previous = _currentQueryId.Value;
            _currentQueryId.Value = queryId;

            return new Popper(previous);
        }

        private sealed class Popper : IDisposable
        {
            private readonly string? _previous;
            private bool _disposed;

            public Popper(string? previous) => _previous = previous;

            public void Dispose()
            {
                if (_disposed)
                    return;

                _currentQueryId.Value = _previous;
                _disposed = true;
            }
        }
    }
}
