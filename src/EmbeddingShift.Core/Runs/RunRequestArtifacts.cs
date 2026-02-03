using System;
using System.Collections.Generic;
using System.Threading;

namespace EmbeddingShift.Core.Runs;

/// <summary>
/// Minimal, replay-focused snapshot describing how a run was produced.
/// This is intentionally small and CLI-oriented.
/// </summary>
public sealed record RunRequest(
    string[] GlobalArgs,
    string[] CommandArgs,
    IReadOnlyDictionary<string, string>? EnvironmentSnapshot = null,
    string? Notes = null);

/// <summary>
/// Persisted alongside run.json as run_request.json.
/// </summary>
public sealed record WorkflowRunRequestArtifact(
    string RunId,
    string WorkflowName,
    DateTimeOffset CreatedUtc,
    RunRequest Request);

/// <summary>
/// Ambient (AsyncLocal) context used by workflows to provide a RunRequest for persistence.
/// </summary>
public static class RunRequestContext
{
    private static readonly AsyncLocal<RunRequest?> _current = new();

    public static RunRequest? Current => _current.Value;

    public static IDisposable Push(RunRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        var previous = _current.Value;
        _current.Value = request;
        return new Pop(previous);
    }

    private sealed class Pop : IDisposable
    {
        private readonly RunRequest? _previous;
        private bool _disposed;

        public Pop(RunRequest? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed) return;
            _current.Value = _previous;
            _disposed = true;
        }
    }
}
