using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Core.Retrieval;

/// <summary>Owns one Python worker; each JSON line scores a whole document batch.</summary>
public sealed class PythonCosineScorer : ICosineScorer
{
    private readonly string _executable;
    private readonly string _scriptPath;
    private readonly TimeSpan _timeout;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private Task<string>? _errors;
    private bool _disposed;
    private bool _failed;

    public PythonCosineScorer(string executable, string scriptPath, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        _executable = executable;
        _scriptPath = Path.GetFullPath(scriptPath);
        if (!File.Exists(_scriptPath))
            throw new FileNotFoundException("Python cosine worker is missing from the application output.", _scriptPath);
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        if (_timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public async Task<double[]> ScoreAsync(float[] query, IReadOnlyList<float[]> documents,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(documents);
        if (query.Length == 0 || query.Any(value => !float.IsFinite(value)))
            throw new ArgumentException("Query must be a non-empty finite vector.", nameof(query));
        foreach (var document in documents)
            if (document is null || document.Length != query.Length || document.Any(value => !float.IsFinite(value)))
                throw new ArgumentException("Documents must contain finite vectors with the query dimension.", nameof(documents));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_failed) throw new InvalidOperationException("This Python cosine worker has failed and cannot be reused.");
            if (documents.Count == 0) return Array.Empty<double>();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_timeout);
            try
            {
                StartWorker();
                var payload = JsonSerializer.Serialize(new { query, documents });
                await _process!.StandardInput.WriteLineAsync(payload.AsMemory(), deadline.Token).ConfigureAwait(false);
                await _process.StandardInput.FlushAsync(deadline.Token).ConfigureAwait(false);
                var line = await _process.StandardOutput.ReadLineAsync(deadline.Token).ConfigureAwait(false);
                if (line is null)
                {
                    await _process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
                    var errors = await _errors!.WaitAsync(deadline.Token).ConfigureAwait(false);
                    throw new InvalidOperationException($"Worker exited with code {_process.ExitCode}: {errors.Trim()}");
                }
                var scores = JsonSerializer.Deserialize<double[]>(line);
                if (scores is null || scores.Length != documents.Count
                    || scores.Any(score => !double.IsFinite(score) || Math.Abs(score) > 1.000001))
                    throw new InvalidOperationException("Worker returned an invalid cosine score batch.");
                return scores;
            }
            catch (OperationCanceledException)
            {
                _failed = true;
                StopWorker();
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Python cosine did not respond within {_timeout.TotalSeconds:0.###} seconds.");
            }
            catch (Exception error) when (error is IOException or JsonException or Win32Exception or InvalidOperationException)
            {
                _failed = true;
                StopWorker();
                throw new InvalidOperationException(
                    $"Python cosine failed: {error.Message} Check python/Setup.ps1 or --python-exe=<path>.", error);
            }
        }
        finally { _gate.Release(); }
    }

    private void StartWorker()
    {
        if (_process is not null) return;
        var start = new ProcessStartInfo(_executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-u");
        start.ArgumentList.Add(_scriptPath);
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        _process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the Python cosine worker.");
        _errors = _process.StandardError.ReadToEndAsync();
    }

    private void StopWorker()
    {
        if (_process is null) return;
        try
        {
            _process.StandardInput.Close();
            if (!_process.WaitForExit(1000))
            {
                _process.Kill();
                _process.WaitForExit(1000);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or IOException) { }
        finally { _process.Dispose(); _process = null; }
    }

    public void Dispose()
    {
        _gate.Wait();
        try
        {
            if (_disposed) return;
            _disposed = true;
            StopWorker();
        }
        finally { _gate.Release(); }
    }
}
