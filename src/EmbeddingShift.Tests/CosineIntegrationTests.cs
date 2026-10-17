using EmbeddingShift.ConsoleEval;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Retrieval;
using EmbeddingShift.Core.Stats;
using EmbeddingShift.Core.Training.PosNeg;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;

namespace EmbeddingShift.Tests;

// Python is optional: enable these integration tests with an explicit interpreter.
public sealed class PythonFactAttribute : FactAttribute
{
    public PythonFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_TEST_PYTHON")))
            Skip = "Set EMBEDDINGSHIFT_TEST_PYTHON to a Python executable with NumPy installed.";
    }
}

public sealed class CosineIntegrationTests
{
    private static string Python => Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_TEST_PYTHON")!;
    private static string Worker => Path.Combine(Path.GetDirectoryName(typeof(CosineIntegrationTests).Assembly.Location)!,
        "python", "cosine_worker.py");

    [Fact]
    public async Task DefaultBackend_UsesTheExistingCSharpArithmeticWithoutStartingPython()
    {
        using var environment = new CosineEnvironment(null, "missing-python-executable");
        using var scorer = CosineScorerFactory.FromEnvironment((q, d) => q[0] + d[0]);
        Assert.IsType<CSharpCosineScorer>(scorer);
        Assert.Equal(new[] { 3.0, 5.0 }, await scorer.ScoreAsync([1f], [[2f], [4f]]));
    }

    [Theory]
    [InlineData("--cosine=invalid")]
    [InlineData("--cosine=")]
    [InlineData("--python-exe=")]
    public void InvalidGlobalOptions_AreRejected(string option) =>
        Assert.Throws<ArgumentException>(() => ConsoleEvalGlobalOptionsParser.Parse([option, "help"]));

    [Fact]
    public void CosineOptions_AreAppliedAndCapturedForReplay()
    {
        using var environment = new CosineEnvironment(null, null);
        var parsed = ConsoleEvalGlobalOptionsParser.Parse(
            ["domain", "mini-insurance", "pipeline", "--cosine=PYTHON", "--python-exe=C:/Python path/python.exe"]);
        Assert.Equal(new[] { "domain", "mini-insurance", "pipeline" }, parsed.CommandArgs);
        ConsoleEvalGlobalEnvironment.Apply(parsed.Options);

        var request = RunRequestFactory.Create(parsed.CommandArgs);
        Assert.Contains("--cosine=python", request.GlobalArgs);
        Assert.Contains("--python-exe=C:/Python path/python.exe", request.GlobalArgs);
        Assert.Equal("python", request.EnvironmentSnapshot![CosineScorerFactory.BackendVariable]);
    }

    [Fact]
    public async Task MissingPython_FailsWithAnActionableErrorWithoutFallback()
    {
        using var environment = new CosineEnvironment("python", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        using var scorer = CosineScorerFactory.FromEnvironment();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => scorer.ScoreAsync([1f], [[1f]]));
        Assert.Contains("Python cosine failed", error.Message);
        Assert.Contains("--python-exe", error.Message);
    }

    [PythonFact]
    public async Task PythonBatch_PreservesDocumentOrderAndHandlesZeroVectorsAcrossRequests()
    {
        using var scorer = new PythonCosineScorer(Python, Worker);
        float[][] documents = [[0f, 1f], [1f, 0f], [-1f, 0f], [0f, 0f], [1f, 1f]];
        var actual = await scorer.ScoreAsync([1f, 0f], documents);
        var expected = new[] { 0.0, 1.0, -1.0, 0.0, 1.0 / Math.Sqrt(2) };
        for (var i = 0; i < expected.Length; i++)
            Assert.InRange(Math.Abs(actual[i] - expected[i]), 0, 1e-12);
        Assert.Equal(new double[documents.Length], await scorer.ScoreAsync([0f, 0f], documents));
    }

    [PythonFact]
    public async Task PythonWorker_IsReusedAndRejectsInvalidProtocolResponses()
    {
        using var worker = new TemporaryWorker("""
            import json, sys
            count = 0
            for line in sys.stdin:
                count += 1
                print(json.dumps([1.0 / count]), flush=True)
            """);
        using var scorer = new PythonCosineScorer(Python, worker.Path);
        Assert.Equal(new[] { 1.0 }, await scorer.ScoreAsync([1f], [[1f]]));
        Assert.Equal(new[] { 0.5 }, await scorer.ScoreAsync([1f], [[1f]]));

        using var invalid = new TemporaryWorker("import sys\nfor line in sys.stdin: print('[0, 1]', flush=True)");
        using var invalidScorer = new PythonCosineScorer(Python, invalid.Path);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => invalidScorer.ScoreAsync([1f], [[1f]]));
        Assert.Contains("invalid cosine score batch", error.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => invalidScorer.ScoreAsync([1f], [[1f]]));
    }

    [PythonFact]
    public async Task UnresponsivePythonWorker_TimesOut()
    {
        using var worker = new TemporaryWorker("import sys, time\nfor line in sys.stdin: time.sleep(60)");
        using var scorer = new PythonCosineScorer(Python, worker.Path, TimeSpan.FromMilliseconds(250));
        await Assert.ThrowsAsync<TimeoutException>(() => scorer.ScoreAsync([1f], [[1f]]));
    }

    [PythonFact]
    public async Task PythonWorker_NotReadingInput_TimesOut()
    {
        using var worker = new TemporaryWorker("import time\ntime.sleep(60)");
        using var scorer = new PythonCosineScorer(Python, worker.Path, TimeSpan.FromMilliseconds(250));
        var query = Enumerable.Repeat(1f, 2048).ToArray();
        var documents = Enumerable.Repeat(query, 256).ToArray();
        await Assert.ThrowsAsync<TimeoutException>(() => scorer.ScoreAsync(query, documents));
    }

    [PythonFact]
    public async Task MiniInsurance_UsesPythonAndPreservesTheSampleRankingsAndMetrics()
    {
        using var environment = new CosineEnvironment("csharp", Python);
        var runner = new StatsAwareWorkflowRunner();
        var csharp = new FileBasedInsuranceMiniWorkflow();
        var reference = await runner.ExecuteAsync("cosine-csharp", csharp);
        Assert.True(reference.Success);

        Environment.SetEnvironmentVariable(CosineScorerFactory.BackendVariable, "python");
        var python = new FileBasedInsuranceMiniWorkflow();
        var actual = await runner.ExecuteAsync("cosine-python", python);
        Assert.True(actual.Success);
        Assert.NotNull(reference.Metrics);
        Assert.NotNull(actual.Metrics);
        Assert.Equal(csharp.PerQuery.Select(q => (q.QueryId, q.Rank, q.TopDocId)),
            python.PerQuery.Select(q => (q.QueryId, q.Rank, q.TopDocId)));
        Assert.Equal(reference.Metrics[MetricKeys.Eval.Map1], actual.Metrics[MetricKeys.Eval.Map1]);
        Assert.Equal(reference.Metrics[MetricKeys.Eval.Ndcg3], actual.Metrics[MetricKeys.Eval.Ndcg3]);

        // An invalid interpreter must also fail through the workflow.
        Environment.SetEnvironmentVariable(CosineScorerFactory.PythonVariable,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new FileBasedInsuranceMiniWorkflow().RunAsync(new InMemoryStatsCollector()));
    }

    [PythonFact]
    public async Task PosNegHardNegativeSelection_UsesTheConfiguredPythonScores()
    {
        using var environment = new CosineEnvironment("csharp", Python);
        var documents = new Dictionary<string, float[]>
        {
            ["negative"] = [1f, 0f],
            ["positive"] = [0f, 1f]
        };
        PosNegTrainingQuery[] queries = [new("q1", "query", "positive")];
        var options = new PosNegLearningOptions(1.5f, false, false);
        var csharp = await PosNegDeltaVectorLearner.LearnAsync(new QueryProvider(), queries, documents, options);
        Assert.Equal(1, csharp.Stats.Cases);

        Environment.SetEnvironmentVariable(CosineScorerFactory.BackendVariable, "python");
        var actual = await PosNegDeltaVectorLearner.LearnAsync(new QueryProvider(), queries, documents, options);
        Assert.Equal(csharp.Stats.Cases, actual.Stats.Cases);
        Assert.Equal(csharp.DeltaVector, actual.DeltaVector);

        Environment.SetEnvironmentVariable(CosineScorerFactory.PythonVariable,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PosNegDeltaVectorLearner.LearnAsync(new QueryProvider(), queries, documents, options));
    }

    private sealed class QueryProvider : IEmbeddingProvider
    {
        public string Name => "cosine-integration";
        public Task<float[]> GetEmbeddingAsync(string text) => Task.FromResult(new[] { 1f, 0f });
    }

    private sealed class CosineEnvironment : IDisposable
    {
        private readonly string? _backend = Environment.GetEnvironmentVariable(CosineScorerFactory.BackendVariable);
        private readonly string? _python = Environment.GetEnvironmentVariable(CosineScorerFactory.PythonVariable);

        public CosineEnvironment(string? backend, string? python)
        {
            Environment.SetEnvironmentVariable(CosineScorerFactory.BackendVariable, backend);
            Environment.SetEnvironmentVariable(CosineScorerFactory.PythonVariable, python);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(CosineScorerFactory.BackendVariable, _backend);
            Environment.SetEnvironmentVariable(CosineScorerFactory.PythonVariable, _python);
        }
    }

    private sealed class TemporaryWorker : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cosine-{Guid.NewGuid():N}.py");
        public TemporaryWorker(string source) => File.WriteAllText(Path, source);
        public void Dispose() => File.Delete(Path);
    }
}
