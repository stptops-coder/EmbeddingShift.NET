using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddingShift.Workflows.Agentic;

/// <summary>
/// Deterministic model-response simulation. It behaves like a downstream model
/// boundary for tests and demos but performs no network call and uses no API key.
/// </summary>
public sealed class SimulatedAgenticReasoningClient : IAgenticReasoningClient
{
    public string Name => "agentic-reasoning-sim";
    public bool IsSimulated => true;

    public Task<AgenticReasoningResult> GenerateAsync(
        AgenticReasoningRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        ct.ThrowIfCancellationRequested();

        var top = request.RetrievedContext.FirstOrDefault();
        var answer = top is null
            ? "Simulated answer: no retrieved context was available."
            : $"Simulated answer: use retrieved candidate '{top.Id}' as the primary context for query '{request.Query}'.";

        var inputText = request.Query + " " + string.Join(" ", request.RetrievedContext.Select(x => x.Text));

        return Task.FromResult(new AgenticReasoningResult(
            Answer: answer,
            BackendName: Name,
            IsSimulated: true,
            EstimatedTokensIn: EstimateTokens(inputText),
            EstimatedTokensOut: EstimateTokens(answer)));
    }

    private static int EstimateTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(value.Length / 4.0));
    }
}
