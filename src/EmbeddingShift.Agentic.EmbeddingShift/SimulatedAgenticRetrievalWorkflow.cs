using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Agentic;
using EmbeddingShift.Core.Shifts;
using EmbeddingShift.Core.Stats;
using EmbeddingShift.Core.Workflows;

namespace EmbeddingShift.Agentic.EmbeddingShift;

/// <summary>
/// Deterministic agentic-ready simulation:
/// a small controller calls EmbeddingShift through a retrieval adaptation boundary,
/// then calls a replaceable downstream reasoning boundary. The default reasoning
/// client is simulated and performs no external model/API call.
/// </summary>
public sealed class SimulatedAgenticRetrievalWorkflow : IWorkflow
{
    private const string ExpectedCandidateId = "policy-cover-water-damage";

    private readonly IRetrievalAdaptationTool _retrievalAdaptationTool;
    private readonly IAgenticReasoningClient _reasoningClient;

    public SimulatedAgenticRetrievalWorkflow()
        : this(CreateDefaultRetrievalTool(), new SimulatedAgenticReasoningClient())
    {
    }

    public SimulatedAgenticRetrievalWorkflow(
        IRetrievalAdaptationTool retrievalAdaptationTool,
        IAgenticReasoningClient reasoningClient)
    {
        _retrievalAdaptationTool = retrievalAdaptationTool
            ?? throw new ArgumentNullException(nameof(retrievalAdaptationTool));
        _reasoningClient = reasoningClient
            ?? throw new ArgumentNullException(nameof(reasoningClient));
    }

    public string Name => "Agentic-Ready-Retrieval-Simulation";

    public static SimulatedAgenticRetrievalWorkflow CreateFromEnvironment()
        => new(CreateDefaultRetrievalTool(), AgenticReasoningClientFactory.FromEnvironment());

    public async Task<WorkflowResult> RunAsync(IStatsCollector stats, CancellationToken ct = default)
    {
        RetrievalAdaptationResult retrievalResult;

        using (stats.TrackStep("AgenticController", "simulated controller decides to call retrieval adaptation"))
        {
            var request = CreateToyRequest();
            stats.RecordExternal(
                "RetrievalAdaptationTool",
                meta: "local embedding-shift retrieval adaptation");

            retrievalResult = await _retrievalAdaptationTool
                .AdaptAsync(request, ct)
                .ConfigureAwait(false);
        }

        var baselineTop1 = retrievalResult.BaselineRanking.Count > 0
            ? retrievalResult.BaselineRanking[0].Id
            : string.Empty;
        var adaptedTop1 = retrievalResult.AdaptedRanking.Count > 0
            ? retrievalResult.AdaptedRanking[0].Id
            : string.Empty;
        var correctedTop1 = string.Equals(adaptedTop1, ExpectedCandidateId, StringComparison.OrdinalIgnoreCase);
        var changedTop1 = !string.Equals(baselineTop1, adaptedTop1, StringComparison.OrdinalIgnoreCase);

        AgenticReasoningResult reasoningResult;
        using (stats.TrackStep("DownstreamReasoning", "replaceable simulated model-response boundary"))
        {
            stats.RecordExternal(
                _reasoningClient.Name,
                tokensIn: 0,
                tokensOut: 0,
                meta: _reasoningClient.IsSimulated
                    ? "simulated reasoning; no network/API call"
                    : "external reasoning client");

            reasoningResult = await _reasoningClient
                .GenerateAsync(new AgenticReasoningRequest(retrievalResult.Query, retrievalResult.AdaptedRanking), ct)
                .ConfigureAwait(false);
        }

        var metrics = new Dictionary<string, double>
        {
            ["agentic.queryCount"] = 1,
            ["agentic.retrievalAdaptation.calls"] = 1,
            ["agentic.reasoning.calls"] = 1,
            ["agentic.reasoning.simulated"] = reasoningResult.IsSimulated ? 1 : 0,
            ["agentic.llmCalls.actual"] = reasoningResult.IsSimulated ? 0 : 1,
            ["agentic.llmCalls.simulated"] = reasoningResult.IsSimulated ? 1 : 0,
            ["agentic.reasoning.estimatedTokensIn"] = reasoningResult.EstimatedTokensIn,
            ["agentic.reasoning.estimatedTokensOut"] = reasoningResult.EstimatedTokensOut,
            ["agentic.shiftCount"] = retrievalResult.AppliedShiftCount,
            ["agentic.top1.changed"] = changedTop1 ? 1 : 0,
            ["agentic.top1.corrected"] = correctedTop1 ? 1 : 0,
            ["agentic.baselineTop1.expected"] = string.Equals(baselineTop1, ExpectedCandidateId, StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            ["agentic.adaptedTop1.expected"] = correctedTop1 ? 1 : 0,
            ["agentic.answer.generated"] = string.IsNullOrWhiteSpace(reasoningResult.Answer) ? 0 : 1
        };

        var notes =
            $"Simulated agentic controller called retrieval adaptation before downstream reasoning. " +
            $"BaselineTop1='{baselineTop1}', AdaptedTop1='{adaptedTop1}', " +
            $"Expected='{ExpectedCandidateId}', ReasoningBackend='{reasoningResult.BackendName}', " +
            $"ReasoningSimulated={reasoningResult.IsSimulated}. No external LLM call was made.";

        return new WorkflowResult(
            Success: correctedTop1 && !string.IsNullOrWhiteSpace(reasoningResult.Answer),
            Metrics: metrics,
            Notes: notes);
    }

    private static IRetrievalAdaptationTool CreateDefaultRetrievalTool()
    {
        // Toy shift: moves the query from a generic/wrong candidate axis toward
        // the expected water-damage policy axis. This keeps the simulation small,
        // deterministic, and independent from an external embedding provider.
        var shift = new FirstShift(
            name: "agentic-ready-query-adaptation",
            shiftVector: new[] { -1.0f, 1.2f, 0.0f });

        return new PipelineRetrievalAdaptationTool(
            new EmbeddingShiftPipeline(new[] { shift }));
    }

    private static RetrievalAdaptationRequest CreateToyRequest()
    {
        var candidates = new[]
        {
            new RetrievalCandidate(
                "policy-general-liability",
                "General liability policy text; intentionally close to the unadapted query.",
                new[] { 1.0f, 0.0f, 0.0f }),

            new RetrievalCandidate(
                ExpectedCandidateId,
                "Household policy section covering water damage and leakage.",
                new[] { 0.0f, 1.0f, 0.0f }),

            new RetrievalCandidate(
                "policy-travel-delay",
                "Travel policy section covering flight delay and baggage issues.",
                new[] { 0.0f, 0.0f, 1.0f })
        };

        return new RetrievalAdaptationRequest(
            Query: "Is water damage from a leaking pipe covered?",
            QueryEmbedding: new[] { 1.0f, 0.0f, 0.0f },
            Candidates: candidates,
            TopK: 3);
    }
}
