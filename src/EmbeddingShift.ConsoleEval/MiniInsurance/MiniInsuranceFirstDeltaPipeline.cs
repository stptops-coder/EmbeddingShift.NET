using System;
using System.Threading;
using System.Threading.Tasks;
using EmbeddingShift.Core.Runs;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;

namespace EmbeddingShift.ConsoleEval.MiniInsurance
{
    /// <summary>
    /// Orchestrates the complete Mini-Insurance First+Delta pipeline:
    /// Baseline -> FirstShift -> First+Delta -> (optional) LearnedDelta
    /// -> Metrics -> persisted artifacts under a stable results/insurance layout.
    /// </summary>
    internal sealed class MiniInsuranceFirstDeltaPipeline
    {
        private readonly Action<string>? _log;
        private readonly StatsAwareWorkflowRunner _wfRunner;

        private WorkflowResult? _baselineResult;
        private WorkflowResult? _firstResult;
        private WorkflowResult? _firstPlusDeltaResult;

        private string? _baselineRunDir;
        private string? _firstRunDir;
        private string? _firstPlusDeltaRunDir;

        public MiniInsuranceFirstDeltaPipeline(Action<string>? log = null)
        {
            _log = log;
            _wfRunner = new StatsAwareWorkflowRunner();
        }

        private void Log(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            _log?.Invoke(message);
            if (_log is null)
            {
                Console.WriteLine($"[MiniInsurancePipeline] {message}");
            }
        }

        public async Task RunAsync(
            bool includeLearnedDelta = true,
            CancellationToken cancellationToken = default)
        {
            var domainRoot = MiniInsurancePaths.GetDomainRoot();
            Log($"Using Mini-Insurance base path: {domainRoot}");

            Log("Step 1: Baseline");
            await RunBaselineAsync(cancellationToken).ConfigureAwait(false);

            Log("Step 2: FirstShift");
            await RunFirstShiftAsync(cancellationToken).ConfigureAwait(false);

            Log("Step 3: First+Delta");
            await RunFirstPlusDeltaAsync(cancellationToken).ConfigureAwait(false);

            if (includeLearnedDelta)
            {
                Log("Step 4: LearnedDelta");
                await RunLearnedDeltaAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                Log("Step 4: LearnedDelta skipped (includeLearnedDelta = false)");
            }

            Log("Step 5: Metrics (aggregate over comparison runs)");
            await ComputeAndPersistMetricsAsync(cancellationToken).ConfigureAwait(false);

            Log("Step 6: Persisting artifacts / final layout");
            await PersistArtifactsAsync(cancellationToken).ConfigureAwait(false);

            Log("Mini-Insurance First+Delta pipeline finished.");
        }

        private async Task RunBaselineAsync(CancellationToken cancellationToken)
        {
            var workflow = new FileBasedInsuranceMiniWorkflow();
            var result = await _wfRunner.ExecuteAsync(
                    "FileBased-Insurance-Mini-Baseline-Pipeline",
                    workflow,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                Log("[Error] Baseline run failed; aborting pipeline.");
                Log(result.ReportMarkdown("Mini Insurance Baseline (Pipeline)"));
                throw new InvalidOperationException("Mini-Insurance baseline run failed.");
            }

            _baselineResult = result;

            var runsRoot = MiniInsurancePaths.GetRunsRoot();
            _baselineRunDir = await RunPersistor.Persist(
                 runsRoot,
                 "FileBased-Insurance-Mini-Baseline-Pipeline",
                 result,
                 cancellationToken)
             .ConfigureAwait(false);

            MiniInsurancePerQueryArtifacts.TryPersist(_baselineRunDir, workflow);

            Log($"Baseline run persisted to:      {_baselineRunDir}");
        }

        private async Task RunFirstShiftAsync(CancellationToken cancellationToken)
        {
            if (_baselineResult is null)
                throw new InvalidOperationException("Baseline result must be computed before FirstShift.");

            var firstPipeline = FileBasedInsuranceMiniWorkflow.CreateFirstShiftPipeline();
            var firstWorkflow = new FileBasedInsuranceMiniWorkflow(firstPipeline);

            var result = await _wfRunner.ExecuteAsync(
                    "FileBased-Insurance-Mini-FirstShift-Pipeline",
                    firstWorkflow,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                Log("[Error] FirstShift run failed; aborting pipeline.");
                Log(result.ReportMarkdown("Mini Insurance FirstShift (Pipeline)"));
                throw new InvalidOperationException("Mini-Insurance FirstShift run failed.");
            }

            _firstResult = result;

            var runsRoot = MiniInsurancePaths.GetRunsRoot();
            _firstRunDir = await RunPersistor.Persist(
                   runsRoot,
                   "FileBased-Insurance-Mini-FirstShift-Pipeline",
                   result,
                   cancellationToken)
               .ConfigureAwait(false);

            MiniInsurancePerQueryArtifacts.TryPersist(_firstRunDir, firstWorkflow);

            Log($"FirstShift run persisted to:   {_firstRunDir}");
        }

        private async Task RunFirstPlusDeltaAsync(CancellationToken cancellationToken)
        {
            if (_baselineResult is null)
                throw new InvalidOperationException("Baseline result must be computed before First+Delta.");

            var pipeline = FileBasedInsuranceMiniWorkflow.CreateFirstPlusDeltaPipeline();
            var workflow = new FileBasedInsuranceMiniWorkflow(pipeline);

            var result = await _wfRunner.ExecuteAsync(
                    "FileBased-Insurance-Mini-FirstPlusDelta-Pipeline",
                    workflow,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                Log("[Error] First+Delta run failed; aborting pipeline.");
                Log(result.ReportMarkdown("Mini Insurance First+Delta (Pipeline)"));
                throw new InvalidOperationException("Mini-Insurance First+Delta run failed.");
            }

            _firstPlusDeltaResult = result;

            var runsRoot = MiniInsurancePaths.GetRunsRoot();
            _firstPlusDeltaRunDir = await RunPersistor.Persist(
                runsRoot,
                "FileBased-Insurance-Mini-FirstPlusDelta-Pipeline",
                result,
                cancellationToken)
            .ConfigureAwait(false);

            MiniInsurancePerQueryArtifacts.TryPersist(_firstPlusDeltaRunDir, workflow);

            Log($"First+Delta run persisted to: {_firstPlusDeltaRunDir}");

            // Persist a comparison object for this run under runsRoot.
            var comparison = EmbeddingShift.ConsoleEval.MiniInsuranceFirstDeltaArtifacts.CreateComparison(
                _baselineResult,
                _firstResult ?? result,
                result,
                _baselineRunDir ?? string.Empty,
                _firstRunDir ?? string.Empty,
                _firstPlusDeltaRunDir ?? string.Empty);

            var comparisonDir = EmbeddingShift.ConsoleEval.MiniInsuranceFirstDeltaArtifacts.PersistComparison(
                runsRoot,
                comparison);

            Log($"First/Delta comparison stored: {comparisonDir}");
        }

        private async Task RunLearnedDeltaAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Learn a global Delta vector from positive/negative policy pairs
            // using the existing MiniInsurancePosNegTrainer (simulation backend).
            Log("[LearnedDelta] Training pos-neg global Delta shift (simulation backend)...");
            var trainingResult = await EmbeddingShift.ConsoleEval.MiniInsurancePosNegTrainer
                .TrainAsync(EmbeddingShift.ConsoleEval.EmbeddingBackend.Sim)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            Log("[LearnedDelta] Training finished.");
            Log($"  Workflow    : {trainingResult.WorkflowName}");
            Log($"  Runs        : {trainingResult.ComparisonRuns}");
            Log($"  Vector dim  : {trainingResult.DeltaVector?.Length ?? 0}");
            Log($"  Results root: {trainingResult.BaseDirectory}");

            var deltaVector = trainingResult.DeltaVector; // float[]
            double normSquared = 0;
            if (deltaVector != null)
            {
                for (var i = 0; i < deltaVector.Length; i++)
                {
                    var v = deltaVector[i];
                    normSquared += v * v;
                }
            }
            var norm = System.Math.Sqrt(normSquared);

            // These improvements are aggregated over the training comparisons
            // (e.g. MAP/NDCG deltas). For the current small Mini-Insurance set
            // they may still be 0.000, but the logging makes future effects visible.
            Log($"  ΔFirst      : {trainingResult.ImprovementFirst:+0.000;-0.000;0.000}");
            Log($"  ΔFirst+Delta: {trainingResult.ImprovementFirstPlusDelta:+0.000;-0.000;0.000}");
            Log($"  Δ(final)    : {trainingResult.DeltaImprovement:+0.000;-0.000;0.000}");
            Log($"  |DeltaVec|  : {norm:0.000000}");

            // Evaluate the learned Delta against the current Mini-Insurance setup.
            // This prints MAP@1 / NDCG@3 deltas to the console.
            Log("[LearnedDelta] Evaluating best learned Delta (pos-neg run)...");
            await EmbeddingShift.ConsoleEval.MiniInsurancePosNegRunner
                .RunAsync(EmbeddingShift.ConsoleEval.EmbeddingBackend.Sim, useLatest: false)
                .ConfigureAwait(false);

            Log("[LearnedDelta] Learned Delta evaluation completed (see MAP/NDCG above).");
        }

        private Task ComputeAndPersistMetricsAsync(CancellationToken cancellationToken)
        {
            var runsRoot = MiniInsurancePaths.GetRunsRoot();
            var aggregatesRoot = MiniInsurancePaths.GetAggregatesRoot();

            try
            {
                var aggregate = EmbeddingShift.ConsoleEval.MiniInsuranceFirstDeltaAggregator
                    .AggregateFromDirectory(runsRoot);

                var aggregateDir = EmbeddingShift.ConsoleEval.MiniInsuranceFirstDeltaAggregator
                    .PersistAggregate(aggregatesRoot, aggregate);

                Log($"Aggregated {aggregate.ComparisonCount} comparison runs.");
                Log($"Aggregate metrics persisted to: {aggregateDir}");
            }
            catch (Exception ex)
            {
                Log($"[Warning] Failed to compute/persist aggregate metrics: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        private Task PersistArtifactsAsync(CancellationToken cancellationToken)
        {
            // No additional steps needed yet: layout decisions already go through
            // MiniInsurancePaths (runs/aggregates/...).
            // We could later add a manifest or a "latest" pointer structure here.
            return Task.CompletedTask;
        }
    }
}
