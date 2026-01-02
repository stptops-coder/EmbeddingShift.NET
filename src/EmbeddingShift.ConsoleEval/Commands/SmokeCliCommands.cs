using System;
using System.Linq;
using System.Threading.Tasks;

namespace EmbeddingShift.ConsoleEval.Commands;

/// <summary>
/// High-level smoke orchestration (no UI/DB; just workflow composition).
/// </summary>
public static class SmokeCliCommands
{
    public static async Task<int> SmokeAllAsync(string[] args, ConsoleEvalHost host)
    {
        // usage:
        //   smoke-all [datasetName]
        //            [--no-reset] [--no-mini] [--no-posneg] [--no-posneg-train] [--no-learned]
        //
        // Defaults:
        //  - reset ON (unless --no-reset)
        //  - baseline ON for demo eval
        //  - mini-insurance pipeline ON
        //  - posneg-train (micro) + posneg-run ON

        if (host is null) throw new ArgumentNullException(nameof(host));

        var dataset = args.Length > 1 && !args[1].StartsWith("-", StringComparison.Ordinal)
            ? args[1]
            : "DemoDataset";

        var noReset = args.Any(a => a.Equals("--no-reset", StringComparison.OrdinalIgnoreCase));
        var noMini = args.Any(a => a.Equals("--no-mini", StringComparison.OrdinalIgnoreCase));
        var noPosNeg = args.Any(a => a.Equals("--no-posneg", StringComparison.OrdinalIgnoreCase));
        var noPosNegTrain = args.Any(a => a.Equals("--no-posneg-train", StringComparison.OrdinalIgnoreCase));
        var noLearned = args.Any(a => a.Equals("--no-learned", StringComparison.OrdinalIgnoreCase));

        Console.WriteLine("[SMOKE-ALL] START");
        Console.WriteLine($"  dataset        = {dataset}");
        Console.WriteLine($"  reset          = {!noReset}");
        Console.WriteLine($"  mini-insurance = {!noMini}");
        Console.WriteLine($"  posneg         = {!noPosNeg}");
        Console.WriteLine($"  posneg-train   = {!noPosNegTrain}");
        Console.WriteLine($"  includeLearned = {!noLearned}");
        Console.WriteLine();

        // 1) Smoke demo: ingest-dataset -> validate -> eval
        {
            Console.WriteLine("[SMOKE-ALL] Step 1/4: run-smoke-demo");
            var smokeDemoArgs = new[] { "run-smoke-demo", dataset }
                .Concat(!noReset ? new[] { "--force-reset" } : Array.Empty<string>())
                .Concat(new[] { "--baseline" })
                .ToArray();

            var rc = await DatasetCliCommands.RunSmokeDemoAsync(smokeDemoArgs, host);
            if (rc != 0)
            {
                Console.WriteLine($"[SMOKE-ALL] FAIL at run-smoke-demo (rc={rc})");
                Environment.ExitCode = rc;
                return rc;
            }
            Console.WriteLine("[SMOKE-ALL] Step 1 OK");
            Console.WriteLine();
        }

        if (!noMini)
        {
            // 2) Mini-insurance pipeline
            Console.WriteLine("[SMOKE-ALL] Step 2/4: domain mini-insurance run");
            var rc = await DomainCliCommands.ExecuteDomainPackAsync(
                "mini-insurance",
                noLearned ? new[] { "run", "--no-learned" } : new[] { "run" });

            if (rc != 0)
            {
                Console.WriteLine($"[SMOKE-ALL] FAIL at mini-insurance run (rc={rc})");
                Environment.ExitCode = rc;
                return rc;
            }
            Console.WriteLine("[SMOKE-ALL] Step 2 OK");
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("[SMOKE-ALL] Step 2 SKIPPED (--no-mini)");
            Console.WriteLine();
        }

        if (!noPosNeg)
        {
            // 3) PosNeg train (micro) – optional
            if (!noPosNegTrain)
            {
                Console.WriteLine("[SMOKE-ALL] Step 3/4: domain mini-insurance posneg-train --mode=micro");
                var rcTrain = await DomainCliCommands.ExecuteDomainPackAsync(
                    "mini-insurance",
                    new[] { "posneg-train", "--mode=micro", "--hardneg-topk=5" });

                if (rcTrain != 0)
                {
                    Console.WriteLine($"[SMOKE-ALL] FAIL at posneg-train (rc={rcTrain})");
                    Environment.ExitCode = rcTrain;
                    return rcTrain;
                }
                Console.WriteLine("[SMOKE-ALL] Step 3 OK");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("[SMOKE-ALL] Step 3 SKIPPED (--no-posneg-train)");
                Console.WriteLine();
            }

            // 4) PosNeg run
            Console.WriteLine("[SMOKE-ALL] Step 4/4: domain mini-insurance posneg-run");
            var rcRun = await DomainCliCommands.ExecuteDomainPackAsync(
                "mini-insurance",
                new[] { "posneg-run" });


            if (rcRun != 0)
            {
                Console.WriteLine($"[SMOKE-ALL] FAIL at posneg-run (rc={rcRun})");
                Environment.ExitCode = rcRun;
                return rcRun;
            }
            Console.WriteLine("[SMOKE-ALL] Step 4 OK");
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("[SMOKE-ALL] Step 3/4 SKIPPED (--no-posneg)");
            Console.WriteLine("[SMOKE-ALL] Step 4/4 SKIPPED (--no-posneg)");
            Console.WriteLine();
        }

        Console.WriteLine("[SMOKE-ALL] PASS");
        return 0;
    }
}
