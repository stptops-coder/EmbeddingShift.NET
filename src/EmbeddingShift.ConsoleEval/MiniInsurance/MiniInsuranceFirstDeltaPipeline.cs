using System;
using System.Threading;
using System.Threading.Tasks;

namespace EmbeddingShift.ConsoleEval.MiniInsurance
{
    /// <summary>
    /// Orchestrates the complete Mini-Insurance First+Delta pipeline:
    /// Baseline -> FirstShift -> First+Delta -> (optional) LearnedDelta
    /// -> Metrics -> Persisted artifacts.
    /// 
    /// For now, all steps are implemented as stubs so that the class
    /// compiles and can be wired up incrementally in later deltas.
    /// </summary>
    internal sealed class MiniInsuranceFirstDeltaPipeline
    {
        private readonly Action<string>? _log;

        public MiniInsuranceFirstDeltaPipeline(Action<string>? log = null)
        {
            _log = log;
        }

        private void Log(string message)
        {
            var line = "[MiniInsurancePipeline] " + message;
            if (_log != null)
            {
                _log(line);
            }
            else
            {
                Console.WriteLine(line);
            }
        }

        /// <summary>
        /// Runs the complete Mini-Insurance First+Delta pipeline.
        /// </summary>
        /// <param name="includeLearnedDelta">
        /// If true, the pipeline will also include the LearnedDelta step.
        /// </param>
        public async Task RunAsync(
            bool includeLearnedDelta = true,
            CancellationToken cancellationToken = default)
        {
            var domainRoot = MiniInsurancePaths.GetDomainRoot();
            Log($"Using Mini-Insurance base path: {domainRoot}");

            Log("Step 1: Baseline");
            await RunBaselineAsync(domainRoot, cancellationToken).ConfigureAwait(false);

            Log("Step 2: FirstShift");
            await RunFirstShiftAsync(domainRoot, cancellationToken).ConfigureAwait(false);

            Log("Step 3: First+Delta");
            await RunFirstPlusDeltaAsync(domainRoot, cancellationToken).ConfigureAwait(false);

            if (includeLearnedDelta)
            {
                Log("Step 4: LearnedDelta");
                await RunLearnedDeltaAsync(domainRoot, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                Log("Step 4: LearnedDelta skipped (includeLearnedDelta = false)");
            }

            Log("Step 5: Metrics");
            await ComputeAndPersistMetricsAsync(domainRoot, cancellationToken).ConfigureAwait(false);

            Log("Step 6: Persisting artifacts / final layout");
            await PersistArtifactsAsync(domainRoot, cancellationToken).ConfigureAwait(false);

            Log("Mini-Insurance First+Delta pipeline finished.");
        }

        // --------------------------------------------------------------------
        // Stub methods – will be wired to the existing Mini-Insurance
        // workflows and commands in the next deltas (Delta 6 / Delta 7).
        // --------------------------------------------------------------------

        private Task RunBaselineAsync(string domainRoot, CancellationToken cancellationToken)
        {
            // TODO: Call existing Mini-Insurance baseline evaluation / run.
            // This method will become the single place orchestrating the baseline run.
            return Task.CompletedTask;
        }

        private Task RunFirstShiftAsync(string domainRoot, CancellationToken cancellationToken)
        {
            // TODO: Call existing Mini-Insurance FirstShift workflow
            // (e.g., FirstShift-only evaluation / run).
            return Task.CompletedTask;
        }

        private Task RunFirstPlusDeltaAsync(string domainRoot, CancellationToken cancellationToken)
        {
            // TODO: Call existing Mini-Insurance First+Delta aggregation / run.
            return Task.CompletedTask;
        }

        private Task RunLearnedDeltaAsync(string domainRoot, CancellationToken cancellationToken)
        {
            // TODO: Call existing LearnedDelta / best-trained delta for Mini-Insurance.
            return Task.CompletedTask;
        }

        private Task ComputeAndPersistMetricsAsync(string domainRoot, CancellationToken cancellationToken)
        {
            // TODO: Consolidate metrics across all previous runs (Baseline, First, First+Delta,
            // LearnedDelta) and persist them under the stable domainRoot.
            return Task.CompletedTask;
        }

        private Task PersistArtifactsAsync(string domainRoot, CancellationToken cancellationToken)
        {
            // TODO: Copy / move / persist all relevant artifacts (runs, training, aggregates,
            // inspection data) into the final folder structure under domainRoot.
            // The structure (runs/, training/, aggregates/, inspect/) will be introduced
            // explicitly in Delta 6.
            return Task.CompletedTask;
        }
    }
}
