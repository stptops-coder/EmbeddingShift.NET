using System;
using System.Threading.Tasks;
using EmbeddingShift.ConsoleEval;             // MiniInsurance* artifacts (sibling namespace)
using EmbeddingShift.Core.Infrastructure;     // DirectoryLayout
using EmbeddingShift.Core.Runs;               // RunPersistor
using EmbeddingShift.Core.Workflows;          // IWorkflow, StatsAwareWorkflowRunner
using EmbeddingShift.Workflows;               // FileBasedInsuranceMiniWorkflow

namespace EmbeddingShift.ConsoleEval.Commands
{
    /// <summary>
    /// Legacy/compat commands for the mini-insurance workflow that were previously implemented inline in Program.cs.
    /// These remain intentionally "console oriented" (Console.WriteLine) but call into the same workflow engine
    /// and persistence helpers as before.
    /// </summary>
    internal static class MiniInsuranceLegacyCliCommands
    {
        public static async Task RunMiniInsuranceAsync()
        {
            Console.WriteLine("[MiniInsurance] Running file-based insurance workflow using sample policies and queries...");

            // Workflow instance as in the tests.
            IWorkflow workflow = new FileBasedInsuranceMiniWorkflow();
            var wfRunner = new StatsAwareWorkflowRunner();

            // Runs like in FileBasedInsuranceMiniWorkflowTests, just under a different run name.
            var result = await wfRunner.ExecuteAsync("FileBased-Insurance-Mini-Console", workflow);

            // Use the central layout helper: /results/insurance (with safe fallbacks).
            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");

            string? persistedPath = null;

            try
            {
                persistedPath = await RunPersistor.Persist(baseDir, result);
                Console.WriteLine($"[MiniInsurance] Results persisted to: {persistedPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MiniInsurance] WARNING: Failed to persist results under '{baseDir}': {ex.Message}");
            }

            if (persistedPath is null)
            {
                Console.WriteLine("[MiniInsurance] WARNING: Could not persist results.");
            }

            Console.WriteLine();
            Console.WriteLine(result.ReportMarkdown("Mini Insurance Evaluation"));
        }

        public static async Task RunMiniInsuranceFirstDeltaAsync()
        {
            Console.WriteLine("[MiniInsurance] Baseline vs FirstShift vs First+Delta (mini workflow)");
            Console.WriteLine();

            var wfRunner = new StatsAwareWorkflowRunner();

            // Baseline: default pipeline (no shifts)
            IWorkflow baselineWorkflow = new FileBasedInsuranceMiniWorkflow();
            var baselineResult = await wfRunner.ExecuteAsync(
                "FileBased-Insurance-Mini-Baseline",
                baselineWorkflow);

            if (!baselineResult.Success)
            {
                Console.WriteLine("[MiniInsurance] Baseline run failed:");
                Console.WriteLine(baselineResult.ReportMarkdown("Mini Insurance Baseline"));
                return;
            }

            // FirstShift only
            var firstPipeline = FileBasedInsuranceMiniWorkflow.CreateFirstShiftPipeline();
            IWorkflow firstWorkflow = new FileBasedInsuranceMiniWorkflow(firstPipeline);
            var firstResult = await wfRunner.ExecuteAsync(
                "FileBased-Insurance-Mini-FirstShift",
                firstWorkflow);

            if (!firstResult.Success)
            {
                Console.WriteLine("[MiniInsurance] FirstShift run failed:");
                Console.WriteLine(firstResult.ReportMarkdown("Mini Insurance FirstShift"));
                return;
            }

            // First + Delta
            var firstDeltaPipeline = FileBasedInsuranceMiniWorkflow.CreateFirstPlusDeltaPipeline();
            IWorkflow firstDeltaWorkflow = new FileBasedInsuranceMiniWorkflow(firstDeltaPipeline);
            var firstDeltaResult = await wfRunner.ExecuteAsync(
                "FileBased-Insurance-Mini-FirstShift-Delta",
                firstDeltaWorkflow);

            if (!firstDeltaResult.Success)
            {
                Console.WriteLine("[MiniInsurance] First+Delta run failed:");
                Console.WriteLine(firstDeltaResult.ReportMarkdown("Mini Insurance First+Delta"));
                return;
            }

            // Persist individual runs under /results/insurance and build a JSON+Markdown comparison.
            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");

            string? baselineRunDir = null;
            string? firstRunDir = null;
            string? firstPlusDeltaRunDir = null;
            string? comparisonDir = null;

            try
            {
                baselineRunDir = await RunPersistor.Persist(baseDir, baselineResult);
                firstRunDir = await RunPersistor.Persist(baseDir, firstResult);
                firstPlusDeltaRunDir = await RunPersistor.Persist(baseDir, firstDeltaResult);

                var comparison = MiniInsuranceFirstDeltaArtifacts.CreateComparison(
                    baselineResult,
                    firstResult,
                    firstDeltaResult,
                    baselineRunDir,
                    firstRunDir,
                    firstPlusDeltaRunDir);

                comparisonDir = MiniInsuranceFirstDeltaArtifacts.PersistComparison(baseDir, comparison);

                Console.WriteLine($"[MiniInsurance] Comparison persisted to: {comparisonDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MiniInsurance] WARNING: Failed to persist First/Delta artifacts under '{baseDir}': {ex.Message}");
            }

            var baselineMetrics = baselineResult.Metrics ?? new System.Collections.Generic.Dictionary<string, double>();
            var firstMetrics = firstResult.Metrics ?? new System.Collections.Generic.Dictionary<string, double>();
            var firstDeltaMetrics = firstDeltaResult.Metrics ?? new System.Collections.Generic.Dictionary<string, double>();

            var allKeys = new System.Collections.Generic.SortedSet<string>(baselineMetrics.Keys);
            allKeys.UnionWith(firstMetrics.Keys);
            allKeys.UnionWith(firstDeltaMetrics.Keys);

            Console.WriteLine();
            Console.WriteLine("[MiniInsurance] Metrics comparison (Baseline vs First vs First+Delta):");
            Console.WriteLine();
            Console.WriteLine("Metric                Baseline    First      First+Delta   ΔFirst-BL   ΔFirst+Delta-BL");
            Console.WriteLine("-------------------   --------    --------   -----------   ---------   ---------------");

            foreach (var key in allKeys)
            {
                baselineMetrics.TryGetValue(key, out var b);
                firstMetrics.TryGetValue(key, out var f);
                firstDeltaMetrics.TryGetValue(key, out var fd);

                var df = f - b;
                var dfd = fd - b;

                Console.WriteLine(
                    $"{key,-19}   {b,8:F3}    {f,8:F3}   {fd,11:F3}   {df,9:+0.000;-0.000;0.000}   {dfd,15:+0.000;-0.000;0.000}");
            }

            Console.WriteLine();
            Console.WriteLine("[MiniInsurance] Done.");
        }

        public static void AggregateFirstDelta()
        {
            Console.WriteLine("[MiniInsurance] Aggregating First/Delta metrics from previous comparison runs...");
            Console.WriteLine();

            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");

            try
            {
                var aggregate = MiniInsuranceFirstDeltaAggregator.AggregateFromDirectory(baseDir);
                var aggregateDir = MiniInsuranceFirstDeltaAggregator.PersistAggregate(baseDir, aggregate);

                Console.WriteLine($"[MiniInsurance] Aggregated {aggregate.ComparisonCount} comparison runs.");
                Console.WriteLine($"[MiniInsurance] Aggregate metrics persisted to: {aggregateDir}");
                Console.WriteLine();

                Console.WriteLine("[MiniInsurance] Metric                 AvgBaseline   AvgFirst   AvgFirst+Delta   ΔFirst-BL   ΔFirst+Delta-BL");
                Console.WriteLine("-------------------   -----------   --------   -----------   ---------   ---------------");

                foreach (var row in aggregate.Metrics)
                {
                    Console.WriteLine(
                        $"{row.Metric,-19}   {row.AverageBaseline,11:F3}   {row.AverageFirst,8:F3}   {row.AverageFirstPlusDelta,11:F3}   {row.AverageDeltaFirstVsBaseline,9:+0.000;-0.000;0.000}   {row.AverageDeltaFirstPlusDeltaVsBaseline,19:+0.000;-0.000;0.000}");
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MiniInsurance] ERROR while aggregating metrics: {ex.Message}");
            }

            Console.WriteLine("[MiniInsurance] Aggregation done.");
        }

        public static void TrainFirstDelta()
        {
            Console.WriteLine("[MiniInsurance] Training Delta shift candidate from aggregated First/Delta metrics...");
            Console.WriteLine();

            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");

            try
            {
                var aggregate = MiniInsuranceFirstDeltaAggregator.AggregateFromDirectory(baseDir);
                var candidate = MiniInsuranceFirstDeltaTrainer.TrainFromAggregate(baseDir, aggregate);
                var trainingDir = MiniInsuranceFirstDeltaTrainer.PersistCandidate(baseDir, candidate);

                Console.WriteLine($"[MiniInsurance] Used {aggregate.ComparisonCount} comparison runs.");
                Console.WriteLine($"[MiniInsurance] Training artifacts persisted to: {trainingDir}");
                Console.WriteLine();

                Console.WriteLine($"[MiniInsurance] Candidate: dims={candidate.DeltaVector.Length}");
                Console.WriteLine("[MiniInsurance] Top deltas (first 8 dims):");

                var vector = candidate.DeltaVector;
                var top = Math.Min(8, vector.Length);

                for (int i = 0; i < top; i++)
                {
                    Console.WriteLine($"  [{i}] = {candidate.DeltaVector[i]:+0.000;-0.000;0.000}");
                }

                Console.WriteLine();
                Console.WriteLine("[MiniInsurance] Training done.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MiniInsurance] ERROR: Training failed under '{baseDir}': {ex.Message}");
            }
        }

        public static async Task RunMiniInsuranceFirstLearnedDeltaAsync()
        {
            Console.WriteLine("[MiniInsurance] Baseline vs FirstShift vs First+LearnedDelta (mini workflow)");
            Console.WriteLine();

            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");

            // Load latest trained Delta candidate.
            var learnedDelta = MiniInsuranceFirstDeltaCandidateLoader
                .LoadLatestDeltaVectorOrDefault(baseDir, out var found);

            if (!found)
            {
                Console.WriteLine("[MiniInsurance] No trained Delta candidate found.");
                Console.WriteLine($"  Looked under: {baseDir}");
                Console.WriteLine("  Run 'mini-insurance-first-delta', then");
                Console.WriteLine("  run 'mini-insurance-first-delta-aggregate' and 'mini-insurance-first-delta-train'.");
                return;
            }

            Console.WriteLine($"[MiniInsurance] Loaded learned Delta vector (dims={learnedDelta.Length}).");
            Console.WriteLine();

            var wfRunner = new StatsAwareWorkflowRunner();

            // Baseline.
            IWorkflow baselineWorkflow = new FileBasedInsuranceMiniWorkflow();
            var baselineResult = await wfRunner.ExecuteAsync(
                "FileBased-Insurance-Mini-Baseline-Learned",
                baselineWorkflow);

            if (!baselineResult.Success)
            {
                Console.WriteLine("[MiniInsurance] Baseline run failed:");
                Console.WriteLine(baselineResult.ReportMarkdown("Mini Insurance Baseline (LearnedDelta)"));
                return;
            }

            // FirstShift only.
            var firstPipeline = FileBasedInsuranceMiniWorkflow.CreateFirstShiftPipeline();
            IWorkflow firstWorkflow = new FileBasedInsuranceMiniWorkflow(firstPipeline);
            var firstResult = await wfRunner.ExecuteAsync(
                "FileBased-Insurance-Mini-FirstShift-Learned",
                firstWorkflow);

            if (!firstResult.Success)
            {
                Console.WriteLine("[MiniInsurance] FirstShift run failed:");
                Console.WriteLine(firstResult.ReportMarkdown("Mini Insurance FirstShift (LearnedDelta)"));
                return;
            }

            // First + learned Delta.
            var learnedPipeline = FileBasedInsuranceMiniWorkflow.CreateFirstPlusDeltaPipeline(learnedDelta);
            IWorkflow learnedWorkflow = new FileBasedInsuranceMiniWorkflow(learnedPipeline);
            var learnedResult = await wfRunner.ExecuteAsync(
                "FileBased-Insurance-Mini-FirstShift-LearnedDelta",
                learnedWorkflow);

            if (!learnedResult.Success)
            {
                Console.WriteLine("[MiniInsurance] LearnedDelta run failed:");
                Console.WriteLine(learnedResult.ReportMarkdown("Mini Insurance First+LearnedDelta"));
                return;
            }

            string? baselineRunDir = null;
            string? firstRunDir = null;
            string? learnedRunDir = null;
            string? comparisonDir = null;

            try
            {
                baselineRunDir = await RunPersistor.Persist(baseDir, baselineResult);
                firstRunDir = await RunPersistor.Persist(baseDir, firstResult);
                learnedRunDir = await RunPersistor.Persist(baseDir, learnedResult);

                var comparison = MiniInsuranceFirstDeltaArtifacts.CreateComparison(
                    baselineResult,
                    firstResult,
                    learnedResult,
                    baselineRunDir,
                    firstRunDir,
                    learnedRunDir);

                comparisonDir = MiniInsuranceFirstDeltaArtifacts.PersistComparison(baseDir, comparison);

                Console.WriteLine($"[MiniInsurance] Comparison persisted to: {comparisonDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MiniInsurance] WARNING: Failed to persist LearnedDelta artifacts under '{baseDir}': {ex.Message}");
            }

            var baselineMetrics = baselineResult.Metrics ?? new System.Collections.Generic.Dictionary<string, double>();
            var firstMetrics = firstResult.Metrics ?? new System.Collections.Generic.Dictionary<string, double>();
            var learnedMetrics = learnedResult.Metrics ?? new System.Collections.Generic.Dictionary<string, double>();

            var allKeys = new System.Collections.Generic.SortedSet<string>(baselineMetrics.Keys);
            allKeys.UnionWith(firstMetrics.Keys);
            allKeys.UnionWith(learnedMetrics.Keys);

            Console.WriteLine();
            Console.WriteLine("[MiniInsurance] Metrics comparison (Baseline vs First vs First+LearnedDelta):");
            Console.WriteLine();
            Console.WriteLine("Metric                Baseline    First      LearnedDelta   ΔFirst-BL   ΔLearnedDelta-BL");
            Console.WriteLine("-------------------   --------    --------   -----------   ---------   ----------------");

            foreach (var key in allKeys)
            {
                baselineMetrics.TryGetValue(key, out var b);
                firstMetrics.TryGetValue(key, out var f);
                learnedMetrics.TryGetValue(key, out var ld);

                var df = f - b;
                var dld = ld - b;

                Console.WriteLine(
                    $"{key,-19}   {b,8:F3}    {f,8:F3}   {ld,11:F3}   {df,9:+0.000;-0.000;0.000}   {dld,16:+0.000;-0.000;0.000}");
            }

            Console.WriteLine();
            Console.WriteLine("[MiniInsurance] Done.");
        }

        public static void InspectFirstDeltaCandidate()
        {
            Console.WriteLine("[MiniInsurance] Inspecting latest trained Delta candidate...");
            Console.WriteLine("  mini-insurance-shift-training-i...spect latest generic shift training result for mini-insurance");
            Console.WriteLine("  mini-insurance-shift-training-h...list recent generic shift training results for mini-insurance");
            Console.WriteLine();

            var baseDir = DirectoryLayout.ResolveResultsRoot("insurance");
            var candidate = MiniInsuranceFirstDeltaCandidateLoader
                .LoadLatestCandidate(baseDir, out var found);

            if (!found || candidate == null)
            {
                Console.WriteLine("[MiniInsurance] No shift candidate found.");
                Console.WriteLine($"  Looked under: {baseDir}");
                Console.WriteLine("  Run 'mini-insurance-first-delta-aggregate' and");
                Console.WriteLine("  then 'mini-insurance-first-delta-train'.");
                return;
            }

            Console.WriteLine($"[MiniInsurance] Candidate: dims={candidate.DeltaVector.Length}");
            Console.WriteLine();

            var vector = candidate.DeltaVector;
            var used = new bool[vector.Length];

            const int topN = 8;

            for (int n = 0; n < topN; n++)
            {
                var bestIdx = -1;
                var bestAbs = 0.0f;

                for (int i = 0; i < vector.Length; i++)
                {
                    if (used[i]) continue;
                    var abs = Math.Abs(vector[i]);
                    if (abs > bestAbs)
                    {
                        bestAbs = abs;
                        bestIdx = i;
                    }
                }

                if (bestIdx < 0 || bestAbs <= 0.0f)
                {
                    break;
                }

                used[bestIdx] = true;
                Console.WriteLine($"  [{bestIdx}] = {vector[bestIdx]:+0.000;-0.000;0.000}");
            }

            Console.WriteLine();
            Console.WriteLine("[MiniInsurance] Candidate inspection done.");
        }
    }
}
